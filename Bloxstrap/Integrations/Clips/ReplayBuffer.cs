using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Bloxstrap.Integrations.Clips
{
    public class ReplayBuffer : IDisposable
    {
        private readonly object _lock = new();
        private readonly LinkedList<FrameData> _frames = new();
        private readonly int _maxFrames;
        private readonly int _targetFps;
        private bool _disposed;

        public int Count
        {
            get { lock (_lock) return _frames.Count; }
        }

        public int MaxFrames => _maxFrames;
        public int TargetFps => _targetFps;

        public ReplayBuffer(int durationSeconds, int fps)
        {
            _targetFps = fps;
            _maxFrames = durationSeconds * fps;
        }

        public void AddFrame(Bitmap frame)
        {
            if (_disposed)
                return;

            var memoryStream = new MemoryStream();
            frame.Save(memoryStream, ImageFormat.Png);
            memoryStream.Position = 0;

            var frameData = new FrameData
            {
                Data = memoryStream.ToArray(),
                Timestamp = DateTime.UtcNow
            };

            memoryStream.Dispose();

            lock (_lock)
            {
                _frames.AddLast(frameData);

                while (_frames.Count > _maxFrames)
                {
                    var first = _frames.First!;
                    _frames.RemoveFirst();
                    first.Value.Data = Array.Empty<byte>();
                }
            }
        }

        public List<FrameData> GetFramesSnapshot()
        {
            lock (_lock)
            {
                return _frames.Select(f => new FrameData
                {
                    Data = f.Data,
                    Timestamp = f.Timestamp
                }).ToList();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                foreach (var frame in _frames)
                    frame.Data = Array.Empty<byte>();

                _frames.Clear();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Clear();
            GC.SuppressFinalize(this);
        }

        public class FrameData
        {
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public DateTime Timestamp { get; set; }
        }
    }
}
