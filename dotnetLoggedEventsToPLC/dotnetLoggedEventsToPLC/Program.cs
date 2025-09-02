using TcEventLoggerAdsProxyLib;
using System.Globalization;
using System.Text.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using TwinCAT;
using TwinCAT.Ads.TypeSystem;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;


// Validate command line arguments
if (args.Length != 2)
{
    Console.WriteLine("Usage: dotnetLoggedEventsToPLC <AMS_NetID> <PLC_Array_Symbol_Path>");
    Console.WriteLine("Example: dotnetLoggedEventsToPLC 39.120.71.102.1.1 MAIN.LoggedEvents");
    return 1;
}

string amsNetId = args[0];
string plcSymbolPath = args[1];

var logger = new TcEventLogger();
AdsClient? adsClient = null;

try
{
    // Connect to the event logger
    logger.Connect(amsNetId);
    
    // Connect to PLC via ADS
    adsClient = new AdsClient();
    adsClient.Connect(amsNetId, 851); // Port 851 for PLC Runtime

    // Read array size from PLC symbol
    int arraySize = 0;
    try
    {
        ISymbolLoader loader = SymbolLoaderFactory.Create(adsClient, SymbolLoaderSettings.Default);
        var arraySymbol = loader.Symbols[plcSymbolPath];

        if (arraySymbol is TwinCAT.Ads.TypeSystem.ArrayInstance arrayInstance)
        {
            arraySize = arrayInstance.Elements.Count;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: Failed to read array dimensions from PLC symbol: {ex.Message}");
        return 1;
    }
    
    // Get logged events limited to array size
    ITcLoggedEventCollection tcLoggedEvents = logger.GetLoggedEvents((uint)arraySize);
    
    // Convert logged events to PLC structure array (limit by array size)
    var plcEvents = new List<ST_ReadEventW>();
    int eventCount = 0;
    foreach (ITcLoggedEvent4 tcLoggedEvent in tcLoggedEvents)
    {
        if (eventCount >= arraySize) break; // Don't exceed array bounds
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
            
            // Set nConfirmState based on WithConfirmation and ConfirmationState
            if (!tcLoggedEvent.WithConfirmation)
            {
                plcEvent.nConfirmState = 0; // Always 0 if confirmation not required
            }
            else
            {
                switch (tcLoggedEvent.ConfirmationState)
                {
                    case TcEventLoggerAdsProxyLib.ConfirmationStateEnum.WaitForConfirmation:
                        plcEvent.nConfirmState = 1;
                        break;
                    case TcEventLoggerAdsProxyLib.ConfirmationStateEnum.Confirmed:
                        plcEvent.nConfirmState = 4;
                        break;
                    case TcEventLoggerAdsProxyLib.ConfirmationStateEnum.Reset:
                        plcEvent.nConfirmState = 3;
                        break;
                    default: // NotSupported, NotRequired
                        plcEvent.nConfirmState = 0;
                        break;
                }
            }
            
            // Set nResetState based on EventType and IsRaised
            if (eventTypeStr.ToLower() == "alarm")
            {
                plcEvent.nResetState = tcLoggedEvent.IsRaised ? 1u : 2u;
            }
            else
            {
                plcEvent.nResetState = 0; // Not an alarm
            }
            
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
            Console.WriteLine($"Error: Failed to map logged event: {ex.Message}");
            return 1;
        }
        
        plcEvents.Add(plcEvent);
        eventCount++;
    }
    
    // Write entire array to PLC using dynamic symbol WriteValue
    if (plcEvents.Count > 0)
    {
        try
        {
            // Create array with proper size (pad with empty structures if needed)
            ST_ReadEventW[] eventArray = new ST_ReadEventW[arraySize];
            
            // Fill array with actual events
            for (int i = 0; i < plcEvents.Count && i < arraySize; i++)
            {
                eventArray[i] = plcEvents[i];
            }
            
            // Fill remaining slots with empty structures if we have fewer events than array size
            for (int i = plcEvents.Count; i < arraySize; i++)
            {
                eventArray[i] = new ST_ReadEventW
                {
                    nSourceID = 0,
                    nEventID = 0,
                    nClass = 0,
                    nConfirmState = 0,
                    nResetState = 0,
                    sSource = ST_ReadEventW.StringToWString("", 256),
                    sDate = ST_ReadEventW.StringToWString("", 24),
                    sTime = ST_ReadEventW.StringToWString("", 24),
                    sComputer = ST_ReadEventW.StringToWString("", 81),
                    sMessageText = ST_ReadEventW.StringToWString("", 256),
                    bQuitMessage = false,
                    bConfirmable = false
                };
            }
            
            // Write entire array using ADS client WriteValue
            adsClient.WriteValue(plcSymbolPath, eventArray);
            
            Console.WriteLine($"Successfully wrote {plcEvents.Count} events to PLC array: {plcSymbolPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Failed to write array to PLC: {ex.Message}");
            return 1;
        }
    }
    else
    {
        Console.WriteLine("No events found to write to PLC");
    }
    
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
    
}