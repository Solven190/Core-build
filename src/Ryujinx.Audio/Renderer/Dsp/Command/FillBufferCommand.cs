using Ryujinx.Audio.Renderer.Server.Splitter;
using System;
using System.Runtime.CompilerServices;

namespace Ryujinx.Audio.Renderer.Dsp.Command
{
    public class FillBufferCommand : ICommand
    {
        public bool Enabled { get; set; }

        public int NodeId { get; }

        public CommandType CommandType => CommandType.FillBuffer;

        public uint EstimatedProcessingTime { get; set; }

        public SplitterDestinationVersion1 Destination1 { get; }
        public SplitterDestinationVersion2 Destination2 { get; }
        public bool IsV2 { get; }
        public int Length { get; }
        public float Value { get; }

        public FillBufferCommand(SplitterDestinationVersion1 destination, int length, float value, int nodeId)
        {
            Enabled = true;
            NodeId = nodeId;

            Destination1 = destination;
            IsV2 = false;
            Length = length;
            Value = value;
        }
        
        public FillBufferCommand(SplitterDestinationVersion2 destination, int length, float value, int nodeId)
        {
            Enabled = true;
            NodeId = nodeId;
            
            Destination2 = destination;
            IsV2 = true;
            Length = length;
            Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessFillBuffer()
        {
            if (IsV2)
            {
                for (int i = 0; i < Length; i++)
                {
                    Destination2.PreviousMixBufferVolume[i] = Value;
                }
            }
            else
            {
                for (int i = 0; i < Length; i++)
                {
                    Destination1.PreviousMixBufferVolume[i] = Value;
                }
            }
        }

        public void Process(CommandList context)
        {
            ProcessFillBuffer();
        }
    }
}
