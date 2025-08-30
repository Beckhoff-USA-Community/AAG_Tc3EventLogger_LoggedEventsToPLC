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
    
    // Read array dimensions using dynamic symbol loader (following TwinCAT ADS Guide)
    int maxArraySize = 80; // Default fallback
    try
    {
        var symbolLoader = (IDynamicSymbolLoader)SymbolLoaderFactory.Create(
            adsClient,
            new SymbolLoaderSettings(SymbolsLoadMode.DynamicTree)
        );

        var symbols = (DynamicSymbolsCollection)symbolLoader.SymbolsDynamic;
        dynamic MAIN = symbols["MAIN"];
        
        // Navigate to the array symbol (e.g., MAIN.LoggedEvents -> LoggedEvents)
        string arrayName = plcSymbolPath.Split('.')[1]; // Extract "LoggedEvents" from "MAIN.LoggedEvents"
        dynamic arraySymbol = MAIN.SubSymbols[arrayName];
        
        if (arraySymbol != null)
        {
            // Get array dimensions metadata as shown in the guide
            var dimensions = arraySymbol.Dimensions;
            if (dimensions != null && dimensions.Count > 0)
            {
                int[] lowerBounds = dimensions.LowerBounds;
                int[] upperBounds = dimensions.UpperBounds;
                int[] dimensionLengths = dimensions.GetDimensionLengths();
                bool isNonZeroBased = dimensions.IsNonZeroBased;
                
                // Use the first dimension's element count
                maxArraySize = dimensionLengths[0];
                
                Console.WriteLine($"Array Metadata:");
                Console.WriteLine($"  - Symbol: {arraySymbol.InstancePath}");
                Console.WriteLine($"  - Lower Bounds: [{string.Join(", ", lowerBounds)}]");
                Console.WriteLine($"  - Upper Bounds: [{string.Join(", ", upperBounds)}]");
                Console.WriteLine($"  - Dimension Lengths: [{string.Join(", ", dimensionLengths)}]");
                Console.WriteLine($"  - Is Non-Zero Based: {isNonZeroBased}");
                Console.WriteLine($"  - Total Elements: {maxArraySize}");
                
                // Display each dimension's element count
                foreach (var dim in dimensions)
                {
                    Console.WriteLine($"  - Dimension Element Count: {dim.ElementCount}");
                }
            }
            else
            {
                Console.WriteLine("Could not access array dimensions, using default size");
            }
        }
        else
        {
            Console.WriteLine($"Could not access symbol: {plcSymbolPath}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not read array dimensions - {ex.Message}");
        Console.WriteLine($"Using default array size: {maxArraySize}");
    }
    
    // Get logged events (limit by array size)
    ITcLoggedEventCollection tcLoggedEvents = logger.GetLoggedEvents((uint)maxArraySize);
    
    // Convert logged events to PLC structure array (limit by array size)
    var plcEvents = new List<ST_ReadEventW>();
    int eventCount = 0;
    foreach (ITcLoggedEvent4 tcLoggedEvent in tcLoggedEvents)
    {
        if (eventCount >= maxArraySize) break; // Don't exceed array bounds
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
            Console.WriteLine($"Warning: Error mapping event - {ex.Message}");
        }
        
        plcEvents.Add(plcEvent);
        eventCount++;
    }
    
    // Write entire array to PLC using dynamic symbol WriteValue
    if (plcEvents.Count > 0)
    {
        try
        {
            // Debug: Check structure size
            int structSize = Marshal.SizeOf<ST_ReadEventW>();
            Console.WriteLine($"C# structure size: {structSize} bytes");
            Console.WriteLine($"Expected PLC size: 1304 bytes");
            Console.WriteLine($"Found {plcEvents.Count} events to write");
            
            // Create array with proper size (pad with empty structures if needed)
            ST_ReadEventW[] eventArray = new ST_ReadEventW[maxArraySize];
            
            // Fill array with actual events
            for (int i = 0; i < plcEvents.Count && i < maxArraySize; i++)
            {
                eventArray[i] = plcEvents[i];
            }
            
            // Fill remaining slots with empty structures if we have fewer events than array size
            for (int i = plcEvents.Count; i < maxArraySize; i++)
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
            
            // Write entire array using ADS client WriteValue (proven working method)
            adsClient.WriteValue(plcSymbolPath, eventArray);
            Console.WriteLine("Used ADS client WriteValue method");
            
            Console.WriteLine($"Successfully wrote entire array of {eventArray.Length} elements to PLC: {plcSymbolPath}");
            Console.WriteLine($"  - {plcEvents.Count} events with data");
            Console.WriteLine($"  - {maxArraySize - plcEvents.Count} empty slots");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing array to PLC - {ex.Message}");
            
            // Fallback to individual element writes if array write fails
            Console.WriteLine("Attempting fallback to individual element writes...");
            for (int i = 0; i < plcEvents.Count; i++)
            {
                try
                {
                    string elementPath = $"{plcSymbolPath}[{i + 1}]"; 
                    adsClient.WriteValue(elementPath, plcEvents[i]);
                    Console.WriteLine($"Wrote event {i + 1} to {elementPath}");
                }
                catch (Exception elemEx)
                {
                    Console.WriteLine($"Error writing event {i + 1} - {elemEx.Message}");
                }
            }
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