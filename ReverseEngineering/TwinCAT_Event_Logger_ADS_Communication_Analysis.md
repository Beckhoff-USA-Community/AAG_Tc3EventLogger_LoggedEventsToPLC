# TwinCAT 3 Event Logger ADS Communication Protocol Analysis

## Executive Summary

This document provides a technical analysis of the ADS (Automation Device Specification) communication protocol used by the TwinCAT 3 Event Logger service. The analysis is based on Wireshark packet captures and logged event data, revealing the communication patterns between the event logger service (port 132) and client applications requesting logged event data.

## Network Architecture

### Communication Participants
- **Event Logger Service**: `39.120.71.102.1.1:132` (TwinCAT Event Logger)
- **Client Application**: `192.168.209.160.1.1` (Dynamic ports: 33039, 32941, 33031)

### Transport Protocol
- **Protocol**: AMS (Automation Message Specification)
- **Communication**: Bidirectional request/response pattern

## ADS Command Structure

### Event Data Retrieval Protocol

#### Bulk Event Query (Primary Request)
```
Command: ADS Read Write Request (Command ID: 9) - Frame 59
IndexGroup: 0x000000c8 (200 decimal)
IndexOffset: 0x00000002
Read Buffer Size: 102,424 bytes
Write Data Size: 16 bytes
Purpose: Retrieve up to 100 events in summary format
Response: Frame 60 (1,678 bytes total, 1,622 bytes of event data)
```

**API Mapping**: This corresponds to `logger.GetLoggedEvents(100)` call
- The "100" parameter is encoded in the 16-byte write data
- 102,424 byte buffer allows ~1KB per event maximum
- Actual response: 1,622 bytes containing 10 events

#### Individual Event Detail Queries
```
Command: ADS Read Write Request (Command ID: 9) - Frames 61, 63, 65, 67, 69, 75, 78, 80, 82, 84
IndexGroup: 0x000001f4 (500 decimal)
IndexOffset: 0x00000000
Read Buffer Size: 2,064 bytes
Write Data Size: 70 bytes
Purpose: Retrieve detailed information for specific events
Responses: Frames 62, 64, 66, 68, 73, 77, 79, 81, 83, 85 (98 bytes each)
```

## Data Structure Analysis

### Event Record Format (from CSV data)
```csv
Alarm State,Severity Level,Event Class Name,Event Id,Unique Id,Event Text,Source Name,Time Raised,Time Cleared,Time Confirmed,Localized Source Name
```

### Sample Event Data
- **Event IDs**: 1-5 (Verbose, Info, Warning, Error, Critical)
- **Unique IDs**: 1-10 (each event has active and cleared states)
- **Source**: MAIN (PLC program)
- **Timestamps**: Windows FILETIME format in hex data
- **Event Pattern**: Each severity level has both active and cleared records

### Binary Data Structure (Frame 60 Analysis)
Frame 60 contains the bulk response with 1,622 bytes of event data:
```
Offset 0x0040-0x0050: Event metadata and counters
Offset 0x0080-0x0090: Timestamp data (Windows FILETIME format: f0 1b f3 57 76 18 dc 01)
Offset 0x00E0-0x00F0: Source name ("MAIN") and event identifiers
Pattern: Repeating structures for each of the 10 events
```

## Communication Flow

### Typical Session Sequence
1. **Data Retrieval Phase**
   - Bulk event query (Frame 59 → Frame 60: up to 100 events)
   - Individual detail queries for returned events (Frames 61, 63, 65... → Frames 62, 64, 66...)
   - Additional metadata requests as needed

### Two-Tier Data Architecture

#### Tier 1: Summary Data (Bulk Response - Frame 60)
- **Size**: ~162 bytes per event average (1,622 bytes ÷ 10 events)
- **Content**: Event IDs, basic timestamps, severity levels
- **Purpose**: Fast overview of available events

#### Tier 2: Detailed Data (Individual Queries - Frames 62, 64, 66, etc.)
- **Buffer Size**: 2KB per event request
- **Actual Response Size**: 98 bytes per event
- **Content**: Full event text, extended metadata, localization
- **Purpose**: Complete event information

## Key Findings

### 1. Scalable Design
The protocol efficiently handles varying event counts (1-100) through dynamic buffer allocation and two-tier data retrieval.

### 2. Efficient Communication
The protocol uses a streamlined approach focused on data retrieval without unnecessary overhead.

### 3. Optimized Data Transfer
- Bulk queries for event discovery
- Individual queries only for detailed information needed
- Compressed timestamp formats (Windows FILETIME)

### 4. Simple Protocol Design
Direct request/response pattern with minimal protocol complexity for efficient event data access.

## Implementation Implications

### For Client Development
1. **Initial Query**: Use IndexGroup 200 with event count limit
2. **Detail Retrieval**: Use IndexGroup 500 for individual event details

### Buffer Sizing Recommendations
- **Bulk Query**: 1KB per expected event minimum
- **Detail Query**: 2KB per event for full information
- **Write Data**: 16 bytes for query parameters, 70 bytes for detail requests

## Security Considerations

The analysis was performed for defensive security purposes to understand legitimate communication patterns. The protocol uses standard ADS/AMS communication without apparent authentication mechanisms beyond network-level access control.

## Conclusion

The TwinCAT 3 Event Logger uses an efficient two-tier ADS communication protocol that balances performance with data completeness. The bulk query provides rapid event discovery, while individual detail queries offer comprehensive event information only when needed. This streamlined design is optimized for historical event retrieval with minimal protocol overhead.

---

**Analysis Date**: August 29, 2025  
**Capture Source**: Wireshark ADS traffic analysis  
**Event Data Source**: TwinCAT Event Logger CSV export  
**Protocol Version**: TwinCAT 3 Event Logger (Port 132)