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

// ============================================================================
// MAIN PROGRAM ENTRY POINT
// ============================================================================

try
{
    var options = ParseArguments(args);
    if (options == null) return 1;

    ValidateArguments(options);

    var (logger, adsClient) = ConnectToSystems(options.AmsNetId);
    
    int arraySize = ValidateAndGetArraySize(adsClient, options.SymbolPath);
    
    var events = GetLoggedEvents(logger, arraySize);
    
    var plcEvents = ProcessEvents(events, arraySize, options.LanguageId, options.DateTimeFormat);
    
    WriteEventsToPlc(adsClient, options.SymbolPath, plcEvents, arraySize);
    
    Console.WriteLine($"Successfully wrote {plcEvents.Count} events to PLC array: {options.SymbolPath}");
    
    CleanupConnections(logger, adsClient);
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}

// ============================================================================
// ARGUMENT PARSING AND VALIDATION
// ============================================================================

static Options? ParseArguments(string[] args)
{
    return Parser.Default.ParseArguments<Options>(args)
        .WithParsed(opts => { })
        .WithNotParsed(errors =>
        {
            Environment.Exit(1);
        })
        .Value;
}

static void ValidateArguments(Options options)
{
    // Validate symbol path format
    if (string.IsNullOrWhiteSpace(options.SymbolPath) || !options.SymbolPath.Contains('.'))
    {
        throw new ArgumentException("Invalid PLC symbol path format. Expected format: MAIN.Variable or GVL.FB.Sub.Variable");
    }
}

// ============================================================================
// PLC CONNECTION AND SYMBOL VALIDATION
// ============================================================================

static (TcEventLogger logger, AdsClient adsClient) ConnectToSystems(string amsNetId)
{
    var logger = new TcEventLogger();
    var adsClient = new AdsClient();
    
    try
    {
        // Connect to the event logger
        logger.Connect(amsNetId);
        
        // Connect to PLC via ADS
        adsClient.Connect(amsNetId, 851); // Port 851 for PLC Runtime
        
        return (logger, adsClient);
    }
    catch
    {
        logger?.Disconnect();
        adsClient?.Disconnect();
        adsClient?.Dispose();
        throw;
    }
}

static int ValidateAndGetArraySize(AdsClient adsClient, string symbolPath)
{
    try
    {
        ISymbolLoader loader = SymbolLoaderFactory.Create(adsClient, SymbolLoaderSettings.Default);
        var arraySymbol = loader.Symbols[symbolPath];

        if (arraySymbol == null)
        {
            throw new InvalidOperationException($"Symbol '{symbolPath}' not found in PLC");
        }

        if (arraySymbol is not TwinCAT.Ads.TypeSystem.ArrayInstance arrayInstance)
        {
            throw new InvalidOperationException($"Symbol '{symbolPath}' is not an array or could not be found");
        }

        // Verify this is an array of ST_ReadEventW structures
        string elementTypeName = arrayInstance.ElementType.Name;
        if (elementTypeName != "ST_ReadEventW" && !elementTypeName.EndsWith(".ST_ReadEventW"))
        {
            throw new InvalidOperationException($"Array element type '{elementTypeName}' is not ST_ReadEventW");
        }

        int arraySize = arrayInstance.SubSymbols.Count;
        Console.WriteLine($"Found array of {arraySize} elements of type: {elementTypeName}");
        
        return arraySize;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to read array dimensions from PLC symbol: {ex.Message}", ex);
    }
}

// ============================================================================
// EVENT PROCESSING
// ============================================================================

static ITcLoggedEventCollection GetLoggedEvents(TcEventLogger logger, int arraySize)
{
    return logger.GetLoggedEvents((uint)arraySize);
}

static List<ST_ReadEventW> ProcessEvents(ITcLoggedEventCollection tcLoggedEvents, int arraySize, uint languageId, E_DateAndTimeFormat dateTimeFormat)
{
    var plcEvents = new List<ST_ReadEventW>();
    int eventCount = 0;
    
    foreach (ITcLoggedEvent4 tcLoggedEvent in tcLoggedEvents)
    {
        if (eventCount >= arraySize) break; // Don't exceed PLC array bounds
        
        try
        {
            var plcEvent = ConvertEventToPlcFormat(tcLoggedEvent, languageId, dateTimeFormat);
            plcEvents.Add(plcEvent);
            eventCount++;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to map logged event: {ex.Message}", ex);
        }
    }
    
    return plcEvents;
}

static ST_ReadEventW ConvertEventToPlcFormat(ITcLoggedEvent4 tcLoggedEvent, uint languageId, E_DateAndTimeFormat dateTimeFormat)
{
    var plcEvent = new ST_ReadEventW();
    
    // Initialize byte arrays to prevent null reference exceptions
    InitializeByteArrays(ref plcEvent);

    // Map basic properties
    MapBasicProperties(ref plcEvent, tcLoggedEvent, languageId);
    
    // Set event class based on type
    SetEventClass(ref plcEvent, tcLoggedEvent);
    
    // Format timestamps based on locale
    FormatTimestamps(ref plcEvent, tcLoggedEvent, dateTimeFormat);
    
    // Set confirmation and reset states
    SetConfirmationState(ref plcEvent, tcLoggedEvent);
    SetResetState(ref plcEvent, tcLoggedEvent);
    
    // Set computer field with severity and class info
    SetComputerField(ref plcEvent, tcLoggedEvent, languageId);

    return plcEvent;
}

static void InitializeByteArrays(ref ST_ReadEventW plcEvent)
{
    plcEvent.sSource = new byte[256 * 2];
    plcEvent.sDate = new byte[24 * 2];
    plcEvent.sTime = new byte[24 * 2];
    plcEvent.sComputer = new byte[81 * 2];
    plcEvent.sMessageText = new byte[256 * 2];
}

static void MapBasicProperties(ref ST_ReadEventW plcEvent, ITcLoggedEvent4 tcLoggedEvent, uint languageId)
{
    string sourceWithPrefix = $"Source: {tcLoggedEvent.SourceName ?? ""}";
    plcEvent.sSource = ST_ReadEventW.StringToWString(sourceWithPrefix, 256);
    plcEvent.nSourceID = tcLoggedEvent.SourceId;
    plcEvent.nEventID = tcLoggedEvent.EventId;

    plcEvent.sMessageText = ST_ReadEventW.StringToWString(tcLoggedEvent.GetText((int)languageId) ?? "", 256);

    plcEvent.bQuitMessage = false;
    plcEvent.bConfirmable = false;
}

static void SetEventClass(ref ST_ReadEventW plcEvent, ITcLoggedEvent4 tcLoggedEvent)
{
    string eventTypeStr = tcLoggedEvent.EventType.ToString();
    plcEvent.nClass = eventTypeStr.ToLower() switch
    {
        "message" => 2u,
        "alarm" => 7u,
        _ => 0u
    };
}

static void FormatTimestamps(ref ST_ReadEventW plcEvent, ITcLoggedEvent4 tcLoggedEvent, E_DateAndTimeFormat dateTimeFormat)
{
    DateTime eventTime = DateTime.FromFileTime(tcLoggedEvent.FileTimeRaised);
    var (dateFormat, timeFormat) = GetDateTimeFormats(dateTimeFormat);
    
    plcEvent.sDate = ST_ReadEventW.StringToWString(eventTime.ToString(dateFormat, CultureInfo.InvariantCulture), 24);
    plcEvent.sTime = ST_ReadEventW.StringToWString(eventTime.ToString(timeFormat, CultureInfo.InvariantCulture), 24);
}

static (string dateFormat, string timeFormat) GetDateTimeFormats(E_DateAndTimeFormat dateTimeFormat)
{
    return dateTimeFormat switch
    {
        E_DateAndTimeFormat.de_DE => ("dd.MM.yyyy", "HH:mm:ss"),
        E_DateAndTimeFormat.en_GB => ("dd/MM/yyyy", "HH:mm:ss"),
        E_DateAndTimeFormat.en_US => ("MM/dd/yyyy", "h:mm:ss tt"),
        _ => ("MM/dd/yyyy", "h:mm:ss tt")
    };
}

static void SetConfirmationState(ref ST_ReadEventW plcEvent, ITcLoggedEvent4 tcLoggedEvent)
{
    if (!tcLoggedEvent.WithConfirmation)
    {
        plcEvent.nConfirmState = 0; // Always 0 if confirmation not required
    }
    else
    {
        plcEvent.nConfirmState = tcLoggedEvent.ConfirmationState switch
        {
            TcEventLoggerAdsProxyLib.ConfirmationStateEnum.WaitForConfirmation => 1u,
            TcEventLoggerAdsProxyLib.ConfirmationStateEnum.Confirmed => 4u,
            TcEventLoggerAdsProxyLib.ConfirmationStateEnum.Reset => 3u,
            _ => 0u // NotSupported, NotRequired
        };
    }
}

static void SetResetState(ref ST_ReadEventW plcEvent, ITcLoggedEvent4 tcLoggedEvent)
{
    string eventTypeStr = tcLoggedEvent.EventType.ToString();
    if (eventTypeStr.ToLower() == "alarm")
    {
        plcEvent.nResetState = tcLoggedEvent.IsRaised ? 1u : 2u;
    }
    else
    {
        plcEvent.nResetState = 0; // Not an alarm
    }
}

static void SetComputerField(ref ST_ReadEventW plcEvent, ITcLoggedEvent4 tcLoggedEvent, uint languageId)
{
    string severity = tcLoggedEvent.SeverityLevel.ToString();
    string className = tcLoggedEvent.GetEventClassName((int)languageId);
    string computerField = $"{severity} | {className}";
    plcEvent.sComputer = ST_ReadEventW.StringToWString(computerField, 81);
}

// ============================================================================
// PLC WRITING
// ============================================================================

static void WriteEventsToPlc(AdsClient adsClient, string symbolPath, List<ST_ReadEventW> plcEvents, int arraySize)
{
    if (plcEvents.Count == 0)
    {
        Console.WriteLine("No events found to write to PLC");
        return;
    }

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
            eventArray[i] = CreateEmptyEvent();
        }
        
        // Write entire array using ADS client WriteValue
        adsClient.WriteValue(symbolPath, eventArray);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to write array to PLC: {ex.Message}", ex);
    }
}

static ST_ReadEventW CreateEmptyEvent()
{
    var emptyEvent = new ST_ReadEventW
    {
        nSourceID = 0,
        nEventID = 0,
        nClass = 0,
        nConfirmState = 0,
        nResetState = 0,
        bQuitMessage = false,
        bConfirmable = false
    };
    
    // Initialize byte arrays
    InitializeByteArrays(ref emptyEvent);
    
    // Set empty strings
    emptyEvent.sSource = ST_ReadEventW.StringToWString("", 256);
    emptyEvent.sDate = ST_ReadEventW.StringToWString("", 24);
    emptyEvent.sTime = ST_ReadEventW.StringToWString("", 24);
    emptyEvent.sComputer = ST_ReadEventW.StringToWString("", 81);
    emptyEvent.sMessageText = ST_ReadEventW.StringToWString("", 256);
    
    return emptyEvent;
}

// ============================================================================
// CLEANUP
// ============================================================================

static void CleanupConnections(TcEventLogger? logger, AdsClient? adsClient)
{
    logger?.Disconnect();
    adsClient?.Disconnect();
    adsClient?.Dispose();

}

// ============================================================================
// DATA STRUCTURES AND ENUMS
// ============================================================================

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