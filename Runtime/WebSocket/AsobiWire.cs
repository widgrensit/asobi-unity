using System;
using System.Collections.Generic;
using System.Text;

namespace Asobi
{
    /// <summary>
    /// One entity delta from a decoded binary <c>world.tick</c> frame.
    /// </summary>
    public sealed class AsobiWireRecord
    {
        /// <summary>"a" added, "u" updated, "r" removed - the same short forms the
        /// JSON wire uses.</summary>
        public string Op;

        /// <summary>
        /// The entity id, or null when the frame's slot has no binding yet.
        /// </summary>
        /// <remarks>
        /// A binding is established by an <c>add</c>, so null means the add that
        /// would have established it was lost. The record is still reported rather
        /// than dropped, because the frame genuinely says this slot changed; the
        /// <c>frame_seq</c> gap that caused it is what drives the resync that
        /// repairs the mapping.
        /// </remarks>
        public string Id;

        /// <summary>The entity's changed fields. Values are float, int, bool,
        /// string or null.</summary>
        public readonly Dictionary<string, object> Fields = new Dictionary<string, object>();
    }

    /// <summary>
    /// One decoded binary <c>world.tick</c> frame.
    /// </summary>
    public sealed class AsobiWireFrame
    {
        /// <summary>The zone these records belong to. <b>Key your entities on
        /// this</b> - see <see cref="AsobiRealtime.WorldResyncAsync"/>.</summary>
        public long ZoneX;

        /// <summary>The second element of the frame's zone.</summary>
        public long ZoneY;

        /// <summary>
        /// Contiguous per zone, advancing only on a frame actually sent, so a jump
        /// by more than one means frames were lost. Null on a frame that holds no
        /// position in the zone's stream, which is the removal list you get for a
        /// zone you are leaving - apply that one ungated.
        /// </summary>
        public long? FrameSeq;

        /// <summary>True when this frame is a complete baseline for its zone
        /// rather than a delta: replace, do not merge. Adopt it unconditionally,
        /// including when <see cref="FrameSeq"/> moves backwards.</summary>
        public bool Kf;

        /// <summary>The server's sim tick.</summary>
        public long Tick;

        /// <summary>The entity deltas, in wire order.</summary>
        public readonly List<AsobiWireRecord> Records = new List<AsobiWireRecord>();
    }

    /// <summary>
    /// Decoder for asobi's binary <c>world.tick</c> frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same information as the JSON frame in roughly a fifth of the bytes, and it
    /// arrives already typed rather than as text you still have to parse - which is
    /// the real saving on this SDK, since <c>OnWorldTick</c> hands you a raw JSON
    /// string and parses none of it.
    /// </para>
    /// <para>
    /// <b>Entity ids are 2-byte slots on the wire, and this decoder resolves them.</b>
    /// A record carries the full id on an <c>add</c> only, which is where the
    /// binding is established; update and remove carry the slot alone. Bindings are
    /// kept per zone, because slot 5 in one zone has nothing to do with slot 5 in
    /// another, and an add REPLACES any binding already there, which is what makes
    /// slot reuse safe.
    /// </para>
    /// <para>
    /// Layout, every multi-byte value little-endian - not the usual choice for a
    /// wire format, and deliberate: Godot's byte readers have no big-endian
    /// counterpart, so the wire follows the runtime with the least room to spare.
    /// </para>
    /// <code>
    /// frame    Kind:8, ZX:32, ZY:32, FrameSeq:64, Kf:8, Tick:64,
    ///          DictLen:8, Dict, RecCount:16, Records
    /// dict     for each name: Len:8, Name/utf8            (at most 32 names)
    /// record   Op:8, Slot:16, [IdLen:8, Id/utf8]?, FieldCount:8, Fields
    /// field    Type:3, Idx:5, Value                       (one header byte)
    /// </code>
    /// </remarks>
    public sealed class AsobiWire
    {
        const byte KindSequenced = 1;
        const byte KindUngated = 2;

        static readonly string[] Ops = { "a", "u", "r" };

        const int TF32 = 0;
        const int TI32 = 1;
        const int TTrue = 2;
        const int TFalse = 3;
        const int TStr = 4;
        const int TNull = 5;

        // The header alone, before any dictionary or record.
        const int MinFrame = 27;

        // Slot -> entity id, one table per zone. A single flat table would alias
        // entities across zones - the same corruption that keying entities by zone
        // exists to prevent.
        readonly Dictionary<long, Dictionary<ushort, string>> _slots =
            new Dictionary<long, Dictionary<ushort, string>>();

        /// <summary>
        /// Forgets every slot binding, for a reconnect.
        /// </summary>
        /// <remarks>
        /// Bindings are established by the adds THIS connection received, so
        /// carrying them over would attach stale ids to slots the server has since
        /// handed to different entities. The keyframe that follows a reconnect
        /// rebuilds the whole table anyway.
        /// </remarks>
        public void Reset() => _slots.Clear();

        /// <summary>
        /// Decodes one frame, or returns null if the bytes are malformed.
        /// </summary>
        /// <remarks>
        /// Null rather than an exception: these bytes come off the network and this
        /// runs on the receive loop, where a throw would tear down the socket.
        /// </remarks>
        public AsobiWireFrame Decode(byte[] bytes, int length)
        {
            try
            {
                return DecodeUnsafe(bytes, length);
            }
            catch (Exception)
            {
                return null;
            }
        }

        AsobiWireFrame DecodeUnsafe(byte[] b, int len)
        {
            if (b == null || len < MinFrame) return null;

            var kind = b[0];
            if (kind != KindSequenced && kind != KindUngated) return null;

            var frame = new AsobiWireFrame
            {
                ZoneX = ReadI32(b, 1),
                ZoneY = ReadI32(b, 5),
                Kf = b[17] != 0,
                Tick = ReadI64(b, 18),
            };
            var frameSeq = ReadI64(b, 9);
            if (kind == KindSequenced)
            {
                frame.FrameSeq = frameSeq;
            }
            else
            {
                // An ungated frame holds no position in the zone's stream. Leaving
                // FrameSeq null says so, where reporting 0 would have every client
                // past its first frame discard the one message that clears its
                // ghosts.
                frame.Kf = false;
            }

            var pos = 26;
            int dictLen = b[pos++];
            var names = new string[dictLen];
            for (var i = 0; i < dictLen; i++)
            {
                if (pos >= len) return null;
                int nameLen = b[pos++];
                if (pos + nameLen > len) return null;
                names[i] = Encoding.UTF8.GetString(b, pos, nameLen);
                pos += nameLen;
            }

            if (pos + 2 > len) return null;
            int recCount = ReadU16(b, pos);
            pos += 2;

            var zoneKey = (frame.ZoneX << 32) ^ (frame.ZoneY & 0xFFFFFFFFL);
            if (!_slots.TryGetValue(zoneKey, out var table))
            {
                table = new Dictionary<ushort, string>();
                _slots[zoneKey] = table;
            }

            for (var r = 0; r < recCount; r++)
            {
                if (pos + 3 > len) return null;
                int opByte = b[pos];
                if (opByte >= Ops.Length) return null;
                var slot = ReadU16(b, pos + 1);
                pos += 3;

                var record = new AsobiWireRecord { Op = Ops[opByte] };
                if (opByte == 0)
                {
                    if (pos >= len) return null;
                    int idLen = b[pos++];
                    if (pos + idLen > len) return null;
                    record.Id = Encoding.UTF8.GetString(b, pos, idLen);
                    pos += idLen;
                    // An add ESTABLISHES the binding and replaces whatever was
                    // there. Slots are reused once freed, so a stale binding
                    // surviving an add would attach the wrong entity to every
                    // later update on that slot.
                    table[slot] = record.Id;
                }
                else if (table.TryGetValue(slot, out var bound))
                {
                    record.Id = bound;
                }

                if (pos >= len) return null;
                int fieldCount = b[pos++];
                for (var f = 0; f < fieldCount; f++)
                {
                    if (pos >= len) return null;
                    int header = b[pos++];
                    var type = header >> 5;
                    var idx = header & 0x1F;
                    if (idx >= names.Length) return null;
                    var key = names[idx];
                    switch (type)
                    {
                        case TF32:
                            if (pos + 4 > len) return null;
                            record.Fields[key] = BitConverter.ToSingle(LittleEndian(b, pos, 4), 0);
                            pos += 4;
                            break;
                        case TI32:
                            if (pos + 4 > len) return null;
                            record.Fields[key] = ReadI32(b, pos);
                            pos += 4;
                            break;
                        case TTrue:
                            record.Fields[key] = true;
                            break;
                        case TFalse:
                            record.Fields[key] = false;
                            break;
                        case TStr:
                            if (pos + 2 > len) return null;
                            var slen = ReadU16(b, pos);
                            pos += 2;
                            if (pos + slen > len) return null;
                            record.Fields[key] = Encoding.UTF8.GetString(b, pos, slen);
                            pos += slen;
                            break;
                        case TNull:
                            record.Fields[key] = null;
                            break;
                        default:
                            return null;
                    }
                }

                // Released only AFTER the record is built, so the frame announcing
                // an entity's departure still carries its id.
                if (opByte == 2) table.Remove(slot);

                frame.Records.Add(record);
            }

            // Trailing bytes mean the frame and this decoder disagree about the
            // layout, and accepting it would hand the game a half-read frame.
            return pos == len ? frame : null;
        }

        // BitConverter follows the host, and Unity targets big-endian platforms
        // (some consoles) as well as little. Copy-and-reverse is the portable
        // answer for the one type with no manual shift path.
        static byte[] LittleEndian(byte[] b, int offset, int count)
        {
            var slice = new byte[count];
            Array.Copy(b, offset, slice, 0, count);
            if (!BitConverter.IsLittleEndian) Array.Reverse(slice);
            return slice;
        }

        static ushort ReadU16(byte[] b, int i) => (ushort)(b[i] | (b[i + 1] << 8));

        static int ReadI32(byte[] b, int i) =>
            b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24);

        static long ReadI64(byte[] b, int i)
        {
            long v = 0;
            for (var k = 7; k >= 0; k--) v = (v << 8) | b[i + k];
            return v;
        }
    }
}
