using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RIO_RESULT
    {
        internal int Status;
        internal uint BytesTransferred;
        internal long ConnectionCorrelation;
        internal long RequestCorrelation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RioNativeOverlapped
    {
        public IntPtr EventHandle;
        public IntPtr InternalHigh;
        public IntPtr InternalLow;
        public int OffsetHigh;
        public int OffsetLow;
        public int SocketIndex;
        public byte Status;
    }

    internal sealed class RIO
    {
        internal WinSock.RIORegisterBuffer RegisterBuffer;
        internal WinSock.RIOCreateCompletionQueue CreateCompletionQueue;
        internal WinSock.RIOCreateRequestQueue CreateRequestQueue;
        internal WinSock.RIOReceive Receive;
        internal WinSock.RIOSend Send;
        internal WinSock.RIONotify Notify;
        internal WinSock.RIOCloseCompletionQueue CloseCompletionQueue;
        internal WinSock.RIODequeueCompletion DequeueCompletion;
        internal WinSock.RIODeregisterBuffer DeregisterBuffer;
        internal WinSock.RIOResizeCompletionQueue ResizeCompletionQueue;
        internal WinSock.RIOResizeRequestQueue ResizeRequestQueue;
        internal const long CachedValue = long.MinValue;
    }


    internal static class RioStatic
    {
        internal static WinSock.RIORegisterBuffer RegisterBuffer;
        internal static WinSock.RIOCreateCompletionQueue CreateCompletionQueue;
        internal static WinSock.RIOCreateRequestQueue CreateRequestQueue;
        internal static WinSock.RIOReceive Receive;
        internal static WinSock.RIOSend Send;
        internal static WinSock.RIONotify Notify;
        internal static WinSock.RIOCloseCompletionQueue CloseCompletionQueue;
        internal static WinSock.RIODequeueCompletion DequeueCompletion;
        internal static WinSock.RIODeregisterBuffer DeregisterBuffer;
        internal static WinSock.RIOResizeCompletionQueue ResizeCompletionQueue;
        internal static WinSock.RIOResizeRequestQueue ResizeRequestQueue;
        internal static WinSock.AcceptEx AcceptEx;
        internal static WinSock.ConnectEx ConnectEx;
        internal static WinSock.DisconnectEx DisconnectEx;

        internal unsafe static void Initalize()
        {

            IntPtr tempSocket;
            tempSocket = WinSock.WSASocket(ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED);
            WinSock.ThrowLastWSAError();

            uint dwBytes = 0;

            Guid AcceptExId = new Guid("B5367DF1-CBAC-11CF-95CA-00805F48A192");
            var acceptExptr = IntPtr.Zero;

            if (WinSock.WSAIoctl2(tempSocket,
                WinSock.SIO_GET_EXTENSION_FUNCTION_POINTER,
                ref AcceptExId,
                16,
                ref acceptExptr,
                IntPtr.Size,
                out dwBytes,
                IntPtr.Zero,
                IntPtr.Zero) != 0)
            {
                WinSock.ThrowLastWSAError();
            }
            else
            {
                AcceptEx = Marshal.GetDelegateForFunctionPointer<WinSock.AcceptEx>(acceptExptr);
            }


            Guid ConnectExId = new Guid("25A207B9-DDF3-4660-8EE9-76E58C74063E");
            var ConnectExptr = IntPtr.Zero;

            if (WinSock.WSAIoctl2(tempSocket,
                WinSock.SIO_GET_EXTENSION_FUNCTION_POINTER,
                ref ConnectExId,
                16,
                ref ConnectExptr,
                IntPtr.Size,
                out dwBytes,
                IntPtr.Zero,
                IntPtr.Zero) != 0)
            {
                WinSock.ThrowLastWSAError();
            }
            else
            {
                ConnectEx = Marshal.GetDelegateForFunctionPointer<WinSock.ConnectEx>(ConnectExptr);
            }


            Guid DisconnectExId = new Guid("7FDA2E11-8630-436F-A031-F536A6EEC157");
            var DisconnectExptr = IntPtr.Zero;

            if (WinSock.WSAIoctl2(tempSocket,
                WinSock.SIO_GET_EXTENSION_FUNCTION_POINTER,
                ref DisconnectExId,
                16,
                ref DisconnectExptr,
                IntPtr.Size,
                out dwBytes,
                IntPtr.Zero,
                IntPtr.Zero) != 0)
            {
                WinSock.ThrowLastWSAError();
            }
            else
            {
                DisconnectEx = Marshal.GetDelegateForFunctionPointer<WinSock.DisconnectEx>(DisconnectExptr);
            }

            var rio = new RIO_EXTENSION_FUNCTION_TABLE();
            Guid RioFunctionsTableId = new Guid("8509e081-96dd-4005-b165-9e2ee8c79e3f");

            if (WinSock.WSAIoctl(tempSocket, WinSock.SIO_GET_MULTIPLE_EXTENSION_FUNCTION_POINTER,
               ref RioFunctionsTableId, 16, ref rio,
               sizeof(RIO_EXTENSION_FUNCTION_TABLE),
               out dwBytes, IntPtr.Zero, IntPtr.Zero) != 0)
            {
                WinSock.ThrowLastWSAError();
            }
            else
            {
                RegisterBuffer = Marshal.GetDelegateForFunctionPointer<WinSock.RIORegisterBuffer>(rio.RIORegisterBuffer);
                CreateCompletionQueue = Marshal.GetDelegateForFunctionPointer<WinSock.RIOCreateCompletionQueue>(rio.RIOCreateCompletionQueue);
                CreateRequestQueue = Marshal.GetDelegateForFunctionPointer<WinSock.RIOCreateRequestQueue>(rio.RIOCreateRequestQueue);
                Notify = Marshal.GetDelegateForFunctionPointer<WinSock.RIONotify>(rio.RIONotify);
                DequeueCompletion = Marshal.GetDelegateForFunctionPointer<WinSock.RIODequeueCompletion>(rio.RIODequeueCompletion);
                Receive = Marshal.GetDelegateForFunctionPointer<WinSock.RIOReceive>(rio.RIOReceive);
                Send = Marshal.GetDelegateForFunctionPointer<WinSock.RIOSend>(rio.RIOSend);
                CloseCompletionQueue = Marshal.GetDelegateForFunctionPointer<WinSock.RIOCloseCompletionQueue>(rio.RIOCloseCompletionQueue);
                DeregisterBuffer = Marshal.GetDelegateForFunctionPointer<WinSock.RIODeregisterBuffer>(rio.RIODeregisterBuffer);
                ResizeCompletionQueue = Marshal.GetDelegateForFunctionPointer<WinSock.RIOResizeCompletionQueue>(rio.RIOResizeCompletionQueue);
                ResizeRequestQueue = Marshal.GetDelegateForFunctionPointer<WinSock.RIOResizeRequestQueue>(rio.RIOResizeRequestQueue);
            }

            WinSock.closesocket(tempSocket);
            WinSock.ThrowLastWSAError();
        }
    }

    internal struct Version
    {
        internal ushort Raw;

        internal Version(byte major, byte minor)
        {
            Raw = major;
            Raw <<= 8;
            Raw += minor;
        }

        internal byte Major
        {
            get
            {
                ushort result = Raw;
                result >>= 8;
                return (byte)result;
            }
        }

        internal byte Minor
        {
            get
            {
                ushort result = Raw;
                result &= 0x00FF;
                return (byte)result;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct RIO_EXTENSION_FUNCTION_TABLE
    {
        internal uint cbSize;

        internal IntPtr RIOReceive;
        internal IntPtr RIOReceiveEx;
        internal IntPtr RIOSend;
        internal IntPtr RIOSendEx;
        internal IntPtr RIOCloseCompletionQueue;
        internal IntPtr RIOCreateCompletionQueue;
        internal IntPtr RIOCreateRequestQueue;
        internal IntPtr RIODequeueCompletion;
        internal IntPtr RIODeregisterBuffer;
        internal IntPtr RIONotify;
        internal IntPtr RIORegisterBuffer;
        internal IntPtr RIOResizeCompletionQueue;
        internal IntPtr RIOResizeRequestQueue;
    }


    [StructLayout(LayoutKind.Sequential)]
    internal struct WSAData
    {
        internal short wVersion;
        internal short wHighVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        internal string szDescription;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
        internal string szSystemStatus;
        internal short iMaxSockets;
        internal short iMaxUdpDg;
        internal IntPtr lpVendorInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct sockaddr_in
    {
        public ADDRESS_FAMILIES sin_family;
        public ushort sin_port;
        public in_addr sin_addr;
        public fixed byte sin_zero[8];
    }

    [StructLayout(LayoutKind.Explicit, Size = 4)]
    internal struct in_addr
    {
        [FieldOffset(0)]
        public byte s_b1;
        [FieldOffset(1)]
        public byte s_b2;
        [FieldOffset(2)]
        public byte s_b3;
        [FieldOffset(3)]
        public byte s_b4;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct RIO_BUFSEGMENT
    {
        public static RIO_BUFSEGMENT* NullSegment = (RIO_BUFSEGMENT*)new IntPtr().ToPointer();

        internal RIO_BUFSEGMENT(IntPtr bufferId, int offset, int length) // should be longs?
        {
            BufferId = bufferId;
            Offset = offset;
            Length = length;
        }

        internal IntPtr BufferId;
        internal int Offset;
        internal int Length;
    }


    [StructLayout(LayoutKind.Sequential)]
    internal struct RIO_NOTIFICATION_COMPLETION
    {
        public RIO_NOTIFICATION_COMPLETION_TYPE Type;
        public RIO_NOTIFICATION_COMPLETION_IOCP Iocp;
    }

    internal enum RIO_NOTIFICATION_COMPLETION_TYPE : int
    {
        POLLING = 0,
        EVENT_COMPLETION = 1,
        IOCP_COMPLETION = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct RIO_NOTIFICATION_COMPLETION_IOCP
    {
        public IntPtr IocpHandle;
        public ulong QueueCorrelation;
        public NativeOverlapped* Overlapped;
    }


    public enum ADDRESS_FAMILIES : short
    {
        AF_UNSPEC = 0,               // unspecified
        AF_UNIX = 1,               // local to host (pipes, portals)
        AF_INET = 2,               // internetwork: UDP, TCP, etc.
        AF_IMPLINK = 3,               // arpanet imp addresses
        AF_PUP = 4,               // pup protocols: e.g. BSP
        AF_CHAOS = 5,               // mit CHAOS protocols
        AF_NS = 6,               // XEROX NS protocols
        AF_IPX = AF_NS,           // IPX protocols: IPX, SPX, etc.
        AF_ISO = 7,               // ISO protocols
        AF_OSI = AF_ISO,          // OSI is ISO
        AF_ECMA = 8,               // european computer manufacturers
        AF_DATAKIT = 9,               // datakit protocols
        AF_CCITT = 10,              // CCITT protocols, X.25 etc
        AF_SNA = 11,              // IBM SNA
        AF_DECnet = 12,              // DECnet
        AF_DLI = 13,              // Direct data link interface
        AF_LAT = 14,              // LAT
        AF_HYLINK = 15,              // NSC Hyperchannel
        AF_APPLETALK = 16,              // AppleTalk
        AF_NETBIOS = 17,              // NetBios-style addresses
        AF_VOICEVIEW = 18,              // VoiceView
        AF_FIREFOX = 19,              // Protocols from Firefox
        AF_UNKNOWN1 = 20,              // Somebody is using this!
        AF_BAN = 21,              // Banyan
        AF_ATM = 22,              // Native ATM Services
        AF_INET6 = 23,              // Internetwork Version 6
        AF_CLUSTER = 24,              // Microsoft Wolfpack
        AF_12844 = 25,              // IEEE 1284.4 WG AF
        AF_IRDA = 26,              // IrDA
        AF_NETDES = 28,              // Network Designers OSI & gateway              
        AF_TCNPROCESS = 29,
        AF_TCNMESSAGE = 30,
        AF_ICLFXBM = 31,
        AF_BTH = 32,              // Bluetooth RFCOMM/L2CAP protocols
        AF_LINK = 33,
        AF_HYPERV = 34,
        AF_MAX = 35,
    }

    public enum SOCKET_TYPE : short
    {
        SOCK_STREAM = 1,
        SOCK_DGRAM = 2,
        SOCK_RAW = 3,
        SOCK_RDM = 4,
        SOCK_SEQPACKET = 5,
    }

    public enum PROTOCOL
    {
        IPPROTO_HOPOPTS = 0,  // IPv6 Hop-by-Hop options
        IPPROTO_ICMP = 1,
        IPPROTO_IGMP = 2,
        IPPROTO_GGP = 3,
        IPPROTO_IPV4 = 4,
        IPPROTO_ST = 5,
        IPPROTO_TCP = 6,
        IPPROTO_CBT = 7,
        IPPROTO_EGP = 8,
        IPPROTO_IGP = 9,
        IPPROTO_PUP = 12,
        IPPROTO_UDP = 17,
        IPPROTO_IDP = 22,
        IPPROTO_RDP = 27,
        IPPROTO_IPV6 = 41, // IPv6 header
        IPPROTO_ROUTING = 43, // IPv6 Routing header
        IPPROTO_FRAGMENT = 44, // IPv6 fragmentation header
        IPPROTO_ESP = 50, // encapsulating security payload
        IPPROTO_AH = 51, // authentication header
        IPPROTO_ICMPV6 = 58, // ICMPv6
        IPPROTO_NONE = 59, // IPv6 no next header
        IPPROTO_DSTOPTS = 60, // IPv6 Destination options
        IPPROTO_ND = 77,
        IPPROTO_ICLFXBM = 78,
        IPPROTO_PIM = 103,
        IPPROTO_PGM = 113,
        IPPROTO_L2TP = 115,
        IPPROTO_SCTP = 132,
        IPPROTO_RAW = 255,
        IPPROTO_MAX = 256,
        //
        //  These are reserved for internal use by Windows.
        //
        IPPROTO_RESERVED_RAW = 257,
        IPPROTO_RESERVED_IPSEC = 258,
        IPPROTO_RESERVED_IPSECOFFLOAD = 259,
        IPPROTO_RESERVED_WNV = 260,
        IPPROTO_RESERVED_MAX = 261
    }

    internal enum SOCKET_FLAGS : uint
    {
        WSA_FLAG_OVERLAPPED = 0x01,
        WSA_FLAG_MULTIPOINT_C_ROOT = 0x02,
        WSA_FLAG_MULTIPOINT_C_LEAF = 0x04,
        WSA_FLAG_MULTIPOINT_D_ROOT = 0x08,
        WSA_FLAG_MULTIPOINT_D_LEAF = 0x10,
        WSA_FLAG_ACCESS_SYSTEM_SECURITY = 0x40,
        WSA_FLAG_NO_HANDLE_INHERIT = 0x80,
        REGISTERED_IO = 0x100
    }

    internal enum RIO_SEND_FLAGS : uint
    {
        NONE = 0x00000000,
        DONT_NOTIFY = 0x00000001,
        DEFER = 0x00000002,
        COMMIT_ONLY = 0x00000008
    }

    internal enum RIO_RECEIVE_FLAGS : uint
    {
        NONE = 0x00000000,
        DONT_NOTIFY = 0x00000001,
        DEFER = 0x00000002,
        WAITALL = 0x00000004,
        COMMIT_ONLY = 0x00000008
    }

    public enum IPPROTO_IP_SocketOptions
    {
        IP_OPTIONS = 1, // Set/get IP options.
        IP_HDRINCL = 2, // Header is included with data.
        IP_TOS = 3, // IP type of service.
        IP_TTL = 4, // IP TTL (hop limit).
        IP_MULTICAST_IF = 9, // IP multicast interface.
        IP_MULTICAST_TTL = 10, // IP multicast TTL (hop limit).
        IP_MULTICAST_LOOP = 11, // IP multicast loopback.
        IP_ADD_MEMBERSHIP = 12, // Add an IP group membership.
        IP_DROP_MEMBERSHIP = 13, // Drop an IP group membership.
        IP_DONTFRAGMENT = 14, // Don't fragment IP datagrams.
        IP_ADD_SOURCE_MEMBERSHIP = 15, // Join IP group/source.
        IP_DROP_SOURCE_MEMBERSHIP = 16, // Leave IP group/source.
        IP_BLOCK_SOURCE = 17, // Block IP group/source.
        IP_UNBLOCK_SOURCE = 18, // Unblock IP group/source.
        IP_PKTINFO = 19, // Receive packet information.
        IP_HOPLIMIT = 21, // Receive packet hop limit.
        IP_RECEIVE_BROADCAST = 22, // Allow/block broadcast reception.
        IP_RECVIF = 24, // Receive arrival interface.
        IP_RECVDSTADDR = 25, // Receive destination address.
        IP_IFLIST = 28, // Enable/Disable an interface list.
        IP_ADD_IFLIST = 29, // Add an interface list entry.
        IP_DEL_IFLIST = 30, // Delete an interface list entry.
        IP_UNICAST_IF = 31, // IP unicast interface.
        IP_RTHDR = 32, // Set/get IPv6 routing header.
        IP_GET_IFLIST = 33, // Get an interface list.
        IP_RECVRTHDR = 38, // Receive the routing header.
        IP_TCLASS = 39, // Packet traffic class.
        IP_RECVTCLASS = 40, // Receive packet traffic class.
        IP_ORIGINAL_ARRIVAL_IF = 47, // Original Arrival Interface Index.
        IP_ECN = 50, // Receive ECN codepoints in the IP header
        IP_PKTINFO_EX = 51, // Receive extended packet information.
        IP_WFP_REDIRECT_RECORDS = 60, // WFP's Connection Redirect Records
        IP_WFP_REDIRECT_CONTEXT = 70, // WFP's Connection Redirect Context
        IP_UNSPECIFIED_TYPE_OF_SERVICE = -1
    }

    public enum IPPROTO_IPV6_SocketOptions
    {
        IPV6_HOPOPTS = 1, // Set/get IPv6 hop-by-hop options.
        IPV6_HDRINCL = 2, // Header is included with data.
        IPV6_UNICAST_HOPS = 4, // IP unicast hop limit.
        IPV6_MULTICAST_IF = 9, // IP multicast interface.
        IPV6_MULTICAST_HOPS = 10, // IP multicast hop limit.
        IPV6_MULTICAST_LOOP = 11, // IP multicast loopback.
        IPV6_ADD_MEMBERSHIP = 12, // Add an IP group membership.
        IPV6_JOIN_GROUP = IPV6_ADD_MEMBERSHIP,
        IPV6_DROP_MEMBERSHIP = 13, // Drop an IP group membership.
        IPV6_LEAVE_GROUP = IPV6_DROP_MEMBERSHIP,
        IPV6_DONTFRAG = 14, // Don't fragment IP datagrams.
        IPV6_PKTINFO = 19, // Receive packet information.
        IPV6_HOPLIMIT = 21, // Receive packet hop limit.
        IPV6_PROTECTION_LEVEL = 23, // Set/get IPv6 protection level.
        IPV6_RECVIF = 24, // Receive arrival interface.
        IPV6_RECVDSTADDR = 25, // Receive destination address.
        IPV6_CHECKSUM = 26, // Offset to checksum for raw IP socket send.
        IPV6_V6ONLY = 27, // Treat wildcard bind as AF_INET6-only.
        IPV6_IFLIST = 28, // Enable/Disable an interface list.
        IPV6_ADD_IFLIST = 29, // Add an interface list entry.
        IPV6_DEL_IFLIST = 30, // Delete an interface list entry.
        IPV6_UNICAST_IF = 31, // IP unicast interface.
        IPV6_RTHDR = 32, // Set/get IPv6 routing header.
        IPV6_GET_IFLIST = 33, // Get an interface list.
        IPV6_RECVRTHDR = 38, // Receive the routing header.
        IPV6_TCLASS = 39, // Packet traffic class.
        IPV6_RECVTCLASS = 40, // Receive packet traffic class.
        IPV6_ECN = 50, // Receive ECN codepoints in the IP header.
        IPV6_PKTINFO_EX = 51, // Receive extended packet information.
        IPV6_WFP_REDIRECT_RECORDS = 60, // WFP's Connection Redirect Records
        IPV6_WFP_REDIRECT_CONTEXT = 70, // WFP's Connection Redirect Context
        IP_UNSPECIFIED_HOP_LIMIT = -1
    }

    public enum IPPROTO_RM_SocketOptions
    { }

    public enum IPPROTO_TCP_SocketOptions
    {
        TCP_NODELAY = 0x0001,
        TCP_EXPEDITED_1122 = 0x0002,
        TCP_KEEPALIVE = 3,
        TCP_MAXSEG = 4,
        TCP_MAXRT = 5,
        TCP_STDURG = 6,
        TCP_NOURG = 7,
        TCP_ATMARK = 8,
        TCP_NOSYNRETRIES = 9,
        TCP_TIMESTAMPS = 10,
        TCP_OFFLOAD_PREFERENCE = 11,
        TCP_CONGESTION_ALGORITHM = 12,
        TCP_DELAY_FIN_ACK = 13,
        TCP_MAXRTMS = 14,
        TCP_BSDURGENT = 0x7000
    }

    public enum IPPROTO_UDP_SocketOptions
    {
        UDP_NOCHECKSUM = 1,
        UDP_CHECKSUM_COVERAGE = 20  /* Set/get UDP-Lite checksum coverage */
    }

    public enum NSPROTO_IPX_SocketOptions
    { }

    public enum SOL_APPLETALK_SocketOptions
    { }

    public enum SOL_IRLMP_SocketOptions
    { }

    public enum SOL_SOCKET_SocketOptions
    {
        SO_DEBUG = 0x0001,         /* turn on debugging info recording */
        SO_ACCEPTCONN = 0x0002,         /* socket has had listen() */
        SO_REUSEADDR = 0x0004,         /* allow local address reuse */
        SO_KEEPALIVE = 0x0008,         /* keep connections alive */
        SO_DONTROUTE = 0x0010,         /* just use interface addresses */
        SO_BROADCAST = 0x0020,         /* permit sending of broadcast msgs */
        SO_USELOOPBACK = 0x0040,         /* bypass hardware when possible */
        SO_LINGER = 0x0080,         /* linger on close if data present */
        SO_OOBINLINE = 0x0100,         /* leave received OOB data in line */
        SO_MAXDG = 0x7009,
        SO_MAXPATHDG = 0x700A,
        SO_UPDATE_ACCEPT_CONTEXT = 0x700B,
        SO_CONNECT_TIME = 0x700C,
        SO_UPDATE_CONNECT_CONTEXT = 0x7010,
        SO_SNDBUF = 0x1001,         /* send buffer size */
        SO_RCVBUF = 0x1002,         /* receive buffer size */
        SO_SNDLOWAT = 0x1003,         /* send low-water mark */
        SO_RCVLOWAT = 0x1004,         /* receive low-water mark */
        SO_SNDTIMEO = 0x1005,         /* send timeout */
        SO_RCVTIMEO = 0x1006,         /* receive timeout */
        SO_ERROR = 0x1007,         /* get error status and clear */
        SO_TYPE = 0x1008,         /* get socket type */
        SO_BSP_STATE = 0x1009,      // get socket 5-tuple state

        SO_GROUP_ID = 0x2001,     /* ID of a socket group */
        SO_GROUP_PRIORITY = 0x2002,     /* the relative priority within a group*/
        SO_MAX_MSG_SIZE = 0x2003,     /* maximum message size */
        SO_PROTOCOL_INFOA = 0x2004,     /* WSAPROTOCOL_INFOA structure */
        SO_PROTOCOL_INFOW = 0x2005,     /* WSAPROTOCOL_INFOW structure */
        PVD_CONFIG = 0x3001,      /* configuration info for service provider */
        SO_CONDITIONAL_ACCEPT = 0x3002,  /* enable true conditional accept: */
                                         /*  connection is not ack-ed to the */
                                         /*  other side until conditional */
                                         /*  function returns CF_ACCEPT */
        SO_PAUSE_ACCEPT = 0x3003,// pause accepting new connections
        SO_COMPARTMENT_ID = 0x3004,// get/set the compartment for a socket
        SO_RANDOMIZE_PORT = 0x3005,// randomize assignment of wildcard ports
        SO_PORT_SCALABILITY = 0x3006,// enable port scalability
        SO_REUSE_UNICASTPORT = 0x3007,// defer ephemeral port allocation for 
                                      // outbound connections
        SO_REUSE_MULTICASTPORT = 0x3008, // enable port reuse and disable unicast 
                                         //reception.

    }


    internal class WinSock
    {
        const string WS2_32 = "WS2_32.dll";

        [DllImport(WS2_32, SetLastError = true)]
        internal static extern unsafe bool WSAGetOverlappedResult(IntPtr socket, [In] RioNativeOverlapped* lpOverlapped, out int lpcbTransfer, bool fWait, out int lpdwFlags);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal delegate IntPtr RIORegisterBuffer([In] IntPtr DataBuffer, [In] uint DataLength);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal delegate void RIODeregisterBuffer([In] IntPtr BufferId);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
        [SuppressUnmanagedCodeSecurity]
        internal unsafe delegate bool RIOSend([In] IntPtr SocketQueue, RIO_BUFSEGMENT* RioBuffer, [In] uint DataBufferCount, [In] RIO_SEND_FLAGS Flags, [In] long RequestCorrelation);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
        [SuppressUnmanagedCodeSecurity]
        internal unsafe delegate bool RIOReceive([In] IntPtr SocketQueue, RIO_BUFSEGMENT* RioBuffer, [In] uint DataBufferCount, [In] RIO_RECEIVE_FLAGS Flags, [In] long RequestCorrelation);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal delegate IntPtr RIOCreateCompletionQueue([In] uint QueueSize, [In] RIO_NOTIFICATION_COMPLETION NotificationCompletion);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal delegate void RIOCloseCompletionQueue([In] IntPtr CQ);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal delegate IntPtr RIOCreateRequestQueue(
                                      [In] IntPtr Socket,
                                      [In] uint MaxOutstandingReceive,
                                      [In] uint MaxReceiveDataBuffers,
                                      [In] uint MaxOutstandingSend,
                                      [In] uint MaxSendDataBuffers,
                                      [In] IntPtr ReceiveCQ,
                                      [In] IntPtr SendCQ,
                                      [In] long ConnectionCorrelation
                                    );

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
        [SuppressUnmanagedCodeSecurity]
        internal delegate uint RIODequeueCompletion([In] IntPtr CQ, [In] IntPtr ResultArray, [In] uint ResultArrayLength);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
        [SuppressUnmanagedCodeSecurity]
        internal delegate Int32 RIONotify([In] IntPtr CQ);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal delegate bool RIOResizeCompletionQueue([In] IntPtr CQ, [In] uint QueueSize);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal delegate bool RIOResizeRequestQueue([In] IntPtr RQ, [In] uint MaxOutstandingReceive, [In] uint MaxOutstandingSend);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
        [SuppressUnmanagedCodeSecurity]
        internal unsafe delegate bool DisconnectEx([In] IntPtr hSocket, [In] RioNativeOverlapped* lpOverlapped, [In] uint dwFlags, [In] uint reserved);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
        [SuppressUnmanagedCodeSecurity]
        internal unsafe delegate bool ConnectEx([In] IntPtr s, [In] sockaddr_in name, [In] int namelen, [In] IntPtr lpSendBuffer, [In] uint dwSendDataLength, [Out] out uint lpdwBytesSent, [In] RioNativeOverlapped* lpOverlapped);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = false)]
        [SuppressUnmanagedCodeSecurity]
        internal unsafe delegate bool AcceptEx([In] IntPtr sListenSocket, [In] IntPtr sAcceptSocket, [In] IntPtr lpOutputBuffer, [In] int dwReceiveDataLength, [In] int dwLocalAddressLength, [In] int dwRemoteAddressLength, [Out] out int lpdwBytesReceived, [In]RioNativeOverlapped* lpOverlapped);

        internal unsafe static RIO Initalize(IntPtr socket)
        {
            uint dwBytes = 0;
            RIO_EXTENSION_FUNCTION_TABLE rio = new RIO_EXTENSION_FUNCTION_TABLE();
            Guid RioFunctionsTableId = new Guid("8509e081-96dd-4005-b165-9e2ee8c79e3f");

            int True = -1;
            var result = setsockopt(socket, IPPROTO_TCP, TCP_NODELAY, (char*)&True, 4);
            if (result != 0)
            {
                var error = WinSock.WSAGetLastError();
                WinSock.WSACleanup();
                throw new Exception(String.Format("ERROR: setsockopt TCP_NODELAY returned {0}", error));
            }

            result = WSAIoctlGeneral(socket, SIO_LOOPBACK_FAST_PATH,
                                &True, 4, null, 0,
                                out dwBytes, IntPtr.Zero, IntPtr.Zero);

            if (result != 0)
            {
                var error = WinSock.WSAGetLastError();
                WinSock.WSACleanup();
                throw new Exception(String.Format("ERROR: WSAIoctl SIO_LOOPBACK_FAST_PATH returned {0}", error));
            }

            result = WSAIoctl(socket, SIO_GET_MULTIPLE_EXTENSION_FUNCTION_POINTER,
               ref RioFunctionsTableId, 16, ref rio,
               sizeof(RIO_EXTENSION_FUNCTION_TABLE),
               out dwBytes, IntPtr.Zero, IntPtr.Zero);

            if (result != 0)
            {
                var error = WinSock.WSAGetLastError();
                WinSock.WSACleanup();
                throw new Exception(String.Format("ERROR: RIOInitalize returned {0}", error));
            }
            else
            {
                RIO rioFunctions = new RIO
                {
                    RegisterBuffer = Marshal.GetDelegateForFunctionPointer<RIORegisterBuffer>(rio.RIORegisterBuffer),
                    CreateCompletionQueue = Marshal.GetDelegateForFunctionPointer<RIOCreateCompletionQueue>(rio.RIOCreateCompletionQueue),
                    CreateRequestQueue = Marshal.GetDelegateForFunctionPointer<RIOCreateRequestQueue>(rio.RIOCreateRequestQueue),
                    Notify = Marshal.GetDelegateForFunctionPointer<RIONotify>(rio.RIONotify),
                    DequeueCompletion = Marshal.GetDelegateForFunctionPointer<RIODequeueCompletion>(rio.RIODequeueCompletion),
                    Receive = Marshal.GetDelegateForFunctionPointer<RIOReceive>(rio.RIOReceive),
                    Send = Marshal.GetDelegateForFunctionPointer<RIOSend>(rio.RIOSend),
                    CloseCompletionQueue = Marshal.GetDelegateForFunctionPointer<RIOCloseCompletionQueue>(rio.RIOCloseCompletionQueue),
                    DeregisterBuffer = Marshal.GetDelegateForFunctionPointer<RIODeregisterBuffer>(rio.RIODeregisterBuffer),
                    ResizeCompletionQueue = Marshal.GetDelegateForFunctionPointer<RIOResizeCompletionQueue>(rio.RIOResizeCompletionQueue),
                    ResizeRequestQueue = Marshal.GetDelegateForFunctionPointer<RIOResizeRequestQueue>(rio.RIOResizeRequestQueue)
                };
                return rioFunctions;
            }
        }
        

        [DllImport(WS2_32, SetLastError = true)]
        internal static extern int WSAIoctl(
          [In] IntPtr socket,
          [In] uint dwIoControlCode,
          [In] ref Guid lpvInBuffer,
          [In] uint cbInBuffer,
          [In, Out] ref RIO_EXTENSION_FUNCTION_TABLE lpvOutBuffer,
          [In] int cbOutBuffer,
          [Out] out uint lpcbBytesReturned,
          [In] IntPtr lpOverlapped,
          [In] IntPtr lpCompletionRoutine
        );

        [DllImport(WS2_32, SetLastError = false)]
        internal static extern int connect([In] IntPtr s, [In] ref sockaddr_in name, [In] int namelen);

        [DllImport(WS2_32, SetLastError = true, EntryPoint = "WSAIoctl")]
        internal unsafe static extern int WSAIoctlGeneral(
          [In] IntPtr socket,
          [In] uint dwIoControlCode,
          [In] int* lpvInBuffer,
          [In] uint cbInBuffer,
          [In] int* lpvOutBuffer,
          [In] int cbOutBuffer,
          [Out] out uint lpcbBytesReturned,
          [In] IntPtr lpOverlapped,
          [In] IntPtr lpCompletionRoutine
        );

        [DllImport(WS2_32, SetLastError = true, EntryPoint = "WSAIoctl")]
        internal unsafe static extern int WSAIoctl2(
          [In] IntPtr socket,
          [In] uint dwIoControlCode,
          [In] ref Guid lpvInBuffer,
          [In] uint cbInBuffer,
          [In, Out] ref IntPtr lpvOutBuffer,
          [In] int cbOutBuffer,
          [Out] out uint lpcbBytesReturned,
          [In] IntPtr lpOverlapped,
          [In] IntPtr lpCompletionRoutine
        );

        [DllImport(WS2_32, SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = true, ThrowOnUnmappableChar = true)]
        internal static extern SocketError WSAStartup([In] short wVersionRequested, [Out] out WSAData lpWSAData);

        [DllImport(WS2_32, SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern IntPtr WSASocket([In] ADDRESS_FAMILIES af, [In] SOCKET_TYPE type, [In] PROTOCOL protocol, [In] IntPtr lpProtocolInfo, [In] Int32 group, [In] SOCKET_FLAGS dwFlags);

        [DllImport(WS2_32, SetLastError = true)]
        internal static extern ushort htons([In] ushort hostshort);

        [DllImport(WS2_32, SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern int bind(IntPtr s, ref sockaddr_in name, int namelen);

        [DllImport(WS2_32, SetLastError = true)]
        internal static extern int listen(IntPtr s, int backlog);

        [DllImport(WS2_32, SetLastError = true)]
        internal unsafe static extern int setsockopt(IntPtr s, int level, int optname, char* optval, int optlen);

        [DllImport(WS2_32, SetLastError = true)]
        internal unsafe static extern int getsockopt(IntPtr s, int level, int optname, char* optval, int* optlen);


        [DllImport(WS2_32, SetLastError = true)]
        internal static extern IntPtr accept(IntPtr s, ref sockaddr_in addr, ref int addrlen);

        [DllImport(WS2_32)]
        internal static extern Int32 WSAGetLastError();

        internal static int ThrowLastWSAError()
        {
            var error = WinSock.WSAGetLastError();
        
            if (error != 0 && error != 997)
            {
                throw new Win32Exception(error);
            }
            else
                return error;
        }

        [DllImport(WS2_32, SetLastError = true)]
        internal static extern Int32 WSACleanup();

        [DllImport(WS2_32, SetLastError = true)]
        internal static extern int closesocket(IntPtr s);

        internal const int SOCKET_ERROR = -1;
        internal const int INVALID_SOCKET = -1;
        internal const uint IOC_OUT = 0x40000000;
        internal const uint IOC_IN = 0x80000000;
        internal const uint IOC_INOUT = IOC_IN | IOC_OUT;
        internal const uint IOC_WS2 = 0x08000000;
        internal const uint IOC_VENDOR = 0x18000000;
        internal const uint SIO_GET_EXTENSION_FUNCTION_POINTER = IOC_INOUT | IOC_WS2 | 6;
        internal const uint SIO_GET_MULTIPLE_EXTENSION_FUNCTION_POINTER = IOC_INOUT | IOC_WS2 | 36;
        internal const uint SIO_LOOPBACK_FAST_PATH = IOC_IN | IOC_WS2 | 16;
        internal const int TCP_NODELAY = 0x0001;


        internal const int IPPROTO_IP = 0;
        internal const int IPPROTO_IPV6 = 41;
        //internal const int IPPROTO_RM = 6;
        internal const int IPPROTO_TCP = 6;
        internal const int IPPROTO_UDP = 17;
        //internal const int NSPROTO_IPX = 6;
        //internal const int SOL_APPLETALK = 6;
        //internal const int SOL_IRLMP = 6;
        internal const int SOL_SOCKET = 0xffff;
    }
}
