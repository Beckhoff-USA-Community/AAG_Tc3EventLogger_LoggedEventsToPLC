using TcEventLoggerAdsProxyLib;
using System.Globalization;
using System.Text.Json;
using System.Reflection;
using TwinCAT.Ads;
using System.Runtime.InteropServices;
using System.Text;

// Validate command line arguments
if (args.Length != 3)
{
    Console.WriteLine("Usage: dotnetLoggedEventsToPLC <AMS_NetID> <NumberOfEvents> <PLC_Symbol_Path>");
    Console.WriteLine("Example: dotnetLoggedEventsToPLC 39.120.71.102.1.1 100 MAIN.LoggedEvents");
    return 1;
}

string amsNetId = args[0];
if (!int.TryParse(args[1], out int numberOfEvents) || numberOfEvents <= 0)
{
    Console.WriteLine("Error: NumberOfEvents must be a positive integer");
    return 1;
}
string plcSymbolPath = args[2];

var logger = new TcEventLogger();
AdsClient? adsClient = null;

try
{
    // Connect to the event logger
    logger.Connect(amsNetId);
    
    // Connect to PLC via ADS
    adsClient = new AdsClient();
    adsClient.Connect(amsNetId, 851); // Port 851 for PLC Runtime
    
    // Get logged events
    ITcLoggedEventCollection tcLoggedEvents = logger.GetLoggedEvents((uint)numberOfEvents);
    
    // Convert logged events to PLC structure array
    var plcEvents = new List<ST_ReadEventW>();
    foreach (ITcLoggedEvent4 tcLoggedEvent in tcLoggedEvents)
    {
        var plcEvent = new ST_ReadEventW();
        
        try
        {

            // Map basic properties using WSTRING byte arrays
            string sourceWithPrefix = $"Source:{tcLoggedEvent.SourceName ?? ""}";
            plcEvent.sSource = ST_ReadEventW.StringToWString(sourceWithPrefix, 256);
            plcEvent.sMessageText = ST_ReadEventW.StringToWString(tcLoggedEvent.GetText(CultureInfo.CurrentCulture.LCID) ?? "", 256);
            
            // Determine event type and set nClass accordingly
            string eventTypeStr = tcLoggedEvent.EventType.ToString();
            switch (eventTypeStr.ToLower())
            {
                case "message":
                    plcEvent.nClass = 2; // Message = 2
                    break;
                case "alarm":
                    plcEvent.nClass = 7; // Alarm = 7
                    break;

            }

            

            // Get severity using the correct property
            string severity = tcLoggedEvent.SeverityLevel.ToString();
            string className = eventTypeStr;
            
            // Get timestamp from FileTimeRaised property
            DateTime eventTime = DateTime.FromFileTime(tcLoggedEvent.FileTimeRaised);
            plcEvent.sDate = ST_ReadEventW.StringToWString(eventTime.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture), 24);
            plcEvent.sTime = ST_ReadEventW.StringToWString(eventTime.ToString("h:mm:ss tt", CultureInfo.InvariantCulture), 24);
            
            // Check if event is confirmed and cleared
            plcEvent.nConfirmState = (tcLoggedEvent.FileTimeConfirmed != 0) ? 1u : 0u;
            plcEvent.nResetState = (tcLoggedEvent.FileTimeCleared != 0) ? 1u : 0u;
            
            // Map other properties via reflection
            var type = tcLoggedEvent.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(tcLoggedEvent);
                    switch (prop.Name.ToLower())
                    {
                        case "sourceid":
                            if (value is uint sourceId) plcEvent.nSourceID = sourceId;
                            break;
                        case "eventid":
                        case "id":
                            if (value is uint eventId) plcEvent.nEventID = eventId;
                            break;
                    }
                }
                catch
                {
                    // Skip properties that can't be mapped
                }
            }
            
            // Set sComputer to contain severity and class name
            string computerField = $"{severity} | {className}";
            plcEvent.sComputer = ST_ReadEventW.StringToWString(computerField, 81);
            
            // Date and time are now set directly from FileTimeRaised, no fallback needed
            
            plcEvent.bQuitMessage = false;
            plcEvent.bConfirmable = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error mapping event - {ex.Message}");
        }
        
        plcEvents.Add(plcEvent);
    }
    
    // Write single event to PLC structure
    if (plcEvents.Count > 0)
    {
        var evt = plcEvents[0]; // Take first event only
        
        try
        {
            // Debug: Check structure size
            int structSize = Marshal.SizeOf<ST_ReadEventW>();
            Console.WriteLine($"C# structure size: {structSize} bytes");
            Console.WriteLine($"Expected PLC size: 1304 bytes");
            Console.WriteLine($"Size difference: {1304 - structSize} bytes");
            
            // Write entire structure in one operation
            adsClient.WriteValue(plcSymbolPath, evt);
            Console.WriteLine($"Successfully wrote event to PLC symbol: {plcSymbolPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing event to PLC - {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine("No events to write to PLC");
    }
    
    // Also output as JSON for verification
    var readableEvents = plcEvents.Select(evt => new
    {
        nSourceID = evt.nSourceID,
        nEventID = evt.nEventID,
        nClass = evt.nClass,
        nConfirmState = evt.nConfirmState,
        nResetState = evt.nResetState,
        sSource = ST_ReadEventW.WStringToString(evt.sSource),
        sDate = ST_ReadEventW.WStringToString(evt.sDate),
        sTime = ST_ReadEventW.WStringToString(evt.sTime),
        sComputer = ST_ReadEventW.WStringToString(evt.sComputer),
        sMessageText = ST_ReadEventW.WStringToString(evt.sMessageText),
        bQuitMessage = evt.bQuitMessage,
        bConfirmable = evt.bConfirmable
    }).ToArray();
    
    var options = new JsonSerializerOptions
    {
        WriteIndented = true
    };
    
    string jsonOutput = JsonSerializer.Serialize(readableEvents, options);
    Console.WriteLine("\nJSON representation:");
    Console.WriteLine(jsonOutput);
    
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}
finally
{
    logger.Disconnect();
    adsClient?.Disconnect();
    adsClient?.Dispose();
}

// Structure matching PLC ST_ReadEventW
[StructLayout(LayoutKind.Sequential, Pack = 0)]
public struct ST_ReadEventW
{
    public uint nSourceID;
    public uint nEventID;
    public uint nClass;
    public uint nConfirmState;
    public uint nResetState;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256 * 2)] // WSTRING(255) = 256 * 2 bytes
    public byte[] sSource;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24 * 2)] // WSTRING(23) = 24 * 2 bytes
    public byte[] sDate;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24 * 2)] // WSTRING(23) = 24 * 2 bytes
    public byte[] sTime;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 81 * 2)] // WSTRING (default 80) = 81 * 2 bytes
    public byte[] sComputer;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256 * 2)] // WSTRING(255) = 256 * 2 bytes
    public byte[] sMessageText;
    
    [MarshalAs(UnmanagedType.U1)]
    public bool bQuitMessage;
    
    [MarshalAs(UnmanagedType.U1)]
    public bool bConfirmable;
    
    // Helper method to convert string to WSTRING byte array
    public static byte[] StringToWString(string str, int maxLength)
    {
        var bytes = new byte[maxLength * 2]; // WSTRING uses 2 bytes per character
        if (!string.IsNullOrEmpty(str))
        {
            var utf16Bytes = Encoding.Unicode.GetBytes(str);
            Array.Copy(utf16Bytes, 0, bytes, 0, Math.Min(utf16Bytes.Length, bytes.Length - 2));
        }
        return bytes;
    }
    
    // Helper method to convert WSTRING byte array back to string
    public static string WStringToString(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return "";
        
        // Find the null terminator (2 bytes of zeros)
        int length = bytes.Length;
        for (int i = 0; i < bytes.Length - 1; i += 2)
        {
            if (bytes[i] == 0 && bytes[i + 1] == 0)
            {
                length = i;
                break;
            }
        }
        
        return Encoding.Unicode.GetString(bytes, 0, length);
    }
}