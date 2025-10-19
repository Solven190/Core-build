using Ryujinx.Cpu;

namespace Ryujinx.HLE.Debugger
{
    /// <summary>
    ///     Marker interface for debugger messages.
    /// </summary>
    interface IMessage;

    public enum MessageType
    {
        Kill,
        BreakIn,
        SendNack
    }

    record struct StatelessMessage(MessageType Type) : IMessage
    {
        public static StatelessMessage Kill => new(MessageType.Kill);
        public static StatelessMessage BreakIn => new(MessageType.BreakIn);
        public static StatelessMessage SendNack => new(MessageType.SendNack);
    }

    struct CommandMessage : IMessage
    {
        public readonly string Command;

        public CommandMessage(string cmd)
        {
            Command = cmd;
        }
    }
    
    public class ThreadBreakMessage : IMessage
    {
        public IExecutionContext Context { get; }
        public ulong Address { get; }
        public int Opcode { get; }

        public ThreadBreakMessage(IExecutionContext context, ulong address, int opcode)
        {
            Context = context;
            Address = address;
            Opcode = opcode;
        }
    }
}
