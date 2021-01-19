using System;
using System.Collections.Generic;
using System.Linq;

namespace Bomberman.Client.ServerSide
{
    public class Packet
    {
        public byte OpCode { get; set; }
        public string Arguments { get; set; }

        public Packet(string opCode, string args="")
        {
            if (!_opCodes.TryGetValue(opCode, out byte code))
                throw new Exception("Unhandled opCode: " + opCode);

            OpCode = code;
            Arguments = args;
        }

        public Packet(byte opCode, string args="")
        {
            OpCode = opCode;
            Arguments = args;
        }

        public override string ToString()
        {
            return string.Format(
                "[Packet:\n" +
                "  OpCode=`{0}`\n" +
                "  Arguments=`{1}`]",
                OpCode, Arguments);
        }

        public byte[] Serialize()
        {
            var bytes = new byte[Arguments.Length + 1];
            bytes[0] = OpCode;
            System.Text.Encoding.ASCII.GetBytes(Arguments).CopyTo(bytes, 1);
            return bytes;
        }

        public static Packet Deserialize(byte[] serializedData)
        {
            return new Packet(serializedData[0], System.Text.Encoding.ASCII.GetString(serializedData.Skip(1).ToArray()));
        }

        private static readonly List<string> _opCodeNames = new List<string>
        {
            "playername",
            "gamecountdown",
            "removefromwaitinglobby",
            "playerdied",
            "heartbeat",
            "bye",
            "joinwaitinglobby",
            "gameover",
            "ready",
            "unready",
            "message",
            "invincibility",
            "gamestart",
            "pickuppowerup",
            "spawnpowerup",
            "detonatePhase2",
            "detonatePhase1",
            "placebombother",
            "placebomb",
            "spawnother",
            "spawn",
            "moveleft",
            "moveright",
            "moveup",
            "movedown"
        };

        private static byte _lastOpCodeNr;
        private static readonly Dictionary<string, byte> _opCodes = _opCodeNames.ToDictionary(a => a, a => _lastOpCodeNr++);
        public static readonly Dictionary<byte, string> ReadableOpCodes = _opCodes.ToDictionary(a => a.Value, a => a.Key);
    }
}
