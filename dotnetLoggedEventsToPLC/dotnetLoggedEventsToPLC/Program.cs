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
using CommandLine;

// Parse command line arguments using CommandLineParser
var options = Parser.Default.ParseArguments<Options>(args)
    .WithParsed(opts => { })
    .WithNotParsed(errors =>
    {
        Environment.Exit(1);
    })
    .Value;

string amsNetId = options.AmsNetId;
string plcSymbolPath = options.SymbolPath;
uint languageId = options.LanguageId;
E_DateAndTimeFormat dateTimeFormat = options.DateTimeFormat;

// Validate symbol path format
if (string.IsNullOrWhiteSpace(plcSymbolPath) || !plcSymbolPath.Contains('.'))
{
    Console.WriteLine("Error: Invalid PLC symbol path format. Expected format: MAIN.Variable or GVL.FB.Sub.Variable");
    return 1;
}

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

        if (arraySymbol == null)
        {
            Console.WriteLine($"Error: Symbol '{plcSymbolPath}' not found in PLC");
            return 1;
        }

        if (arraySymbol is TwinCAT.Ads.TypeSystem.ArrayInstance arrayInstance)
        {
            // Verify this is an array of ST_ReadEventW structures
            string elementTypeName = arrayInstance.ElementType.Name;
            if (elementTypeName == "ST_ReadEventW" || elementTypeName.EndsWith(".ST_ReadEventW"))
            {
                arraySize = arrayInstance.Elements.Count;        
            }
            else
            {
                Console.WriteLine($"Error: Array element type '{arrayInstance.ElementType.Name}' is not ST_ReadEventW");
                return 1;
            }
        }
        else
        {
            Console.WriteLine($"Error: Symbol '{plcSymbolPath}' is not an array or could not be found");
            return 1;
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
        if (eventCount >= arraySize) break; // Don't exceed PLC array bounds
        var plcEvent = new ST_ReadEventW();
        
        try
        {

            // Map basic properties using WSTRING byte arrays
            string sourceWithPrefix = $"Source: {tcLoggedEvent.SourceName ?? ""}";
            plcEvent.sSource = ST_ReadEventW.StringToWString(sourceWithPrefix, 256);
            plcEvent.sMessageText = ST_ReadEventW.StringToWString(tcLoggedEvent.GetText((int)languageId) ?? "", 256);
            
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

            // Get severity
            string severity = tcLoggedEvent.SeverityLevel.ToString();
            // Get class name
            string className = tcLoggedEvent.GetEventClassName((int)languageId);

            // Get timestamp from FileTimeRaised property and format based on locale
            DateTime eventTime = DateTime.FromFileTime(tcLoggedEvent.FileTimeRaised);
            string dateFormat, timeFormat;
            
            switch (dateTimeFormat)
            {
                case E_DateAndTimeFormat.de_DE:
                    dateFormat = "dd.MM.yyyy";
                    timeFormat = "HH:mm:ss";
                    break;
                case E_DateAndTimeFormat.en_GB:
                    dateFormat = "dd/MM/yyyy";
                    timeFormat = "HH:mm:ss";
                    break;
                case E_DateAndTimeFormat.en_US:
                default:
                    dateFormat = "MM/dd/yyyy";
                    timeFormat = "h:mm:ss tt";
                    break;
            }
            
            plcEvent.sDate = ST_ReadEventW.StringToWString(eventTime.ToString(dateFormat, CultureInfo.InvariantCulture), 24);
            plcEvent.sTime = ST_ReadEventW.StringToWString(eventTime.ToString(timeFormat, CultureInfo.InvariantCulture), 24);
            
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

            // Set sComputer to contain severity and class name
            string computerField = $"{severity} | {className}";
            plcEvent.sComputer = ST_ReadEventW.StringToWString(computerField, 81);

            
            plcEvent.nSourceID = tcLoggedEvent.SourceId;
            plcEvent.nEventID = tcLoggedEvent.EventId;
             
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
    
    // Write entire array to PLC using symbol WriteValue
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

// Enum to match PLC E_DateAndTimeFormat
public enum E_DateAndTimeFormat
{
    de_DE = 0,
    en_GB = 1, 
    en_US = 2
}

// Command line options class
public class Options
{
    [Option("amsnetid", Required = true, HelpText = "TwinCAT AMS Net ID (e.g., 39.120.71.102.1.1)")]
    public string AmsNetId { get; set; } = string.Empty;

    [Option("symbolpath", Required = true, HelpText = "Full path to LoggedEvents array in PLC (e.g., MAIN.fbReadTc3Events.LoggedEvents)")]
    public string SymbolPath { get; set; } = string.Empty;

    [Option("languageid", Required = true, HelpText = "Language ID (1033=English, 1031=German, 2057=English UK)")]
    public uint LanguageId { get; set; }

    [Option("datetimeformat", Required = true, HelpText = "DateTime format enum value (0=de_DE, 1=en_GB, 2=en_US)")]
    public E_DateAndTimeFormat DateTimeFormat { get; set; }
}