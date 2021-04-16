using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using AvaloniaGif.Decoding;
using System.Linq;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Platform;
using SprinterPublishing;

namespace AvaloniaGif
{
    internal sealed class ApngBackgroundWorker
    {
        private static readonly Stopwatch _timer = Stopwatch.StartNew();
        private APNG _apng;
        private IReadOnlyList<Bitmap> Frames => _apng.;
        public Bitmap CurentFrame { get; set; }

        private Task _bgThread;
        private BgWorkerState _state;
        private readonly object _lockObj;
        private readonly Queue<BgWorkerCommand> _cmdQueue;
        private volatile bool _shouldStop;
        private int _iterationCount;

        private GifRepeatBehavior _repeatBehavior;
        public GifRepeatBehavior IterationCount
        {
            get => _repeatBehavior;
            set
            {
                lock (_lockObj)
                {
                    InternalSeek(0, true);
                    ResetPlayVars();
                    _state = BgWorkerState.Paused;
                    _repeatBehavior = value;
                }
            }
        }

        public Action CurrentFrameChanged;
        private int _currentIndex;

        public int CurrentFrameIndex
        {
            get => _currentIndex;
            set
            {
                if (value != _currentIndex)
                    lock (_lockObj)
                        InternalSeek(value, true);
            }
        }

        private void ResetPlayVars()
        {
            _iterationCount = 0;
            CurrentFrameIndex = -1;
        }

        private void RefreshBitmapCache()
        {

        }

        private void InternalSeek(int value, bool isManual)
        {
            int lowerBound = 0;

            // Skip already rendered frames if the seek position is above the previous frame index.
            if (isManual & value > _currentIndex)
            {
                // Only render the new seeked frame if the delta
                // seek position is just 1 frame.
                if (value - _currentIndex == 1)
                {
                    CurentFrame = new Bitmap(Frames[value].GetStream());
                    SetIndexVal(value, isManual);
                    return;
                }
                lowerBound = _currentIndex;
            }

            for (int fI = lowerBound; fI <= value; fI++)
            {
                var targetFrame = new Bitmap(Frames[fI].GetStream());

                //// Ignore frames with restore disposal method except the current one.
                //if (fI != value & targetFrame.FrameDisposalMethod == FrameDisposal.Restore)
                //    continue;

                CurentFrame = targetFrame;
            }

            SetIndexVal(value, isManual);
        }

        private void SetIndexVal(int value, bool isManual)
        {
            _currentIndex = value;

            if (isManual)
            {
                if (_state == BgWorkerState.Complete)
                {
                    _state = BgWorkerState.Paused;
                    _iterationCount = 0;
                }

                CurrentFrameChanged?.Invoke();
            }
        }

        public ApngBackgroundWorker(APNG apng)
        {
            _apng = apng;
            _lockObj = new object();
            _repeatBehavior = new GifRepeatBehavior() { LoopForever = true };
            _cmdQueue = new Queue<BgWorkerCommand>();

            // Save the color table cache ID's to refresh them on cache while
            // the image is either stopped/paused.
            //_colorTableIDList = _apng.Frames
            //                              .Where(p => p.IsLocalColorTableUsed)
            //                              .Select(p => p.LocalColorTableCacheID)
            //                              .ToList();

            //if (_gifDecoder.Header.HasGlobalColorTable)
            //    _colorTableIDList.Add(_gifDecoder.Header.GlobalColorTableCacheID);

            ResetPlayVars();

            _bgThread = Task.Factory.StartNew(MainLoop, CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Current);
        }

        public void SendCommand(BgWorkerCommand cmd)
        {
            ShowFirstFrame();
            lock (_lockObj)
                _cmdQueue.Enqueue(cmd);
        }

        public BgWorkerState GetState()
        {
            lock (_lockObj)
            {
                var ret = _state;
                return ret;
            }
        }

        private void MainLoop()
        {
            while (true)
            {
                if (_shouldStop)
                {
                    DoDispose();
                    break;
                }

                CheckCommands();
                DoStates();
            }
        }

        private void DoStates()
        {
            switch (_state)
            {
                case BgWorkerState.Null:
                    Thread.Sleep(40);
                    break;
                case BgWorkerState.Paused:
                    //RefreshColorTableCache();
                    Thread.Sleep(60);
                    break;
                case BgWorkerState.Start:
                    _state = BgWorkerState.Running;
                    break;
                case BgWorkerState.Running:
                    WaitAndRenderNext();
                    break;
                case BgWorkerState.Complete:
                    //RefreshColorTableCache();
                    Thread.Sleep(60);
                    break;
            }
        }

        private void CheckCommands()
        {
            BgWorkerCommand cmd;

            lock (_lockObj)
            {
                if (_cmdQueue.Count <= 0) return;
                cmd = _cmdQueue.Dequeue();
            }

            switch (cmd)
            {
                case BgWorkerCommand.Dispose:
                    DoDispose();
                    break;
                case BgWorkerCommand.Play:
                    switch (_state)
                    {
                        case BgWorkerState.Null:
                            _state = BgWorkerState.Start;
                            break;
                        case BgWorkerState.Paused:
                            _state = BgWorkerState.Running;
                            break;
                        case BgWorkerState.Complete:
                            ResetPlayVars();
                            _state = BgWorkerState.Start;
                            break;
                    }
                    break;
                case BgWorkerCommand.Pause:
                    switch (_state)
                    {
                        case BgWorkerState.Running:
                            _state = BgWorkerState.Paused;
                            break;
                    }
                    break;
            }

        }

        private void DoDispose()
        {
            _state = BgWorkerState.Dispose;
            _shouldStop = true;
            //_apng.Dispose();
        }

        private void ShowFirstFrame()
        {
            if (_shouldStop) return;
            CurentFrame = new Bitmap(Frames[0].GetStream());
        }

        private void WaitAndRenderNext()
        {
            if (!IterationCount.LoopForever & _iterationCount > IterationCount.Count)
            {
                _state = BgWorkerState.Complete;
                return;
            }

            _currentIndex = (_currentIndex + 1) % Frames.Count;

            CurrentFrameChanged?.Invoke();

            var targetDelay = Frames[_currentIndex].FrameDelay;

            var t1 = _timer.Elapsed;

            CurentFrame = new Bitmap(Frames[_currentIndex].GetStream());

            var t2 = _timer.Elapsed;
            var delta = t2 - t1;

            if (delta > targetDelay) return;
            Thread.Sleep(targetDelay);

            if (!IterationCount.LoopForever & _currentIndex == 0)
                _iterationCount++;
        }

        //private void RenderFrame(int i)
        //{
        //    var frame = Frames[i];
        //    var fcTlChunk = frame.fcTLChunk;
        //    Bitmap foregroundBitmap;

        //    var frameBitmap = new Bitmap(frame.GetStream());

        //    if (fcTlChunk.XOffset == 0 &&
        //        fcTlChunk.YOffset == 0 &&
        //        fcTlChunk.Width == frameBitmap.PixelSize.Width &&
        //        fcTlChunk.Height == frameBitmap.PixelSize.Height &&
        //        fcTlChunk.BlendOp == BlendOps.APNGBlendOpSource)
        //    {
        //        foregroundBitmap = frameBitmap;
        //    }
        //    else
        //    {
        //        foregroundBitmap = CurentFrame ?? new WriteableBitmap(frameBitmap.PixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        //        if (foregroundBitmap is WriteableBitmap w)
        //        {
        //            foregroundBitmap = frameBitmap;
        //        }

        //        // blend_op 
        //        switch (fcTlChunk.BlendOp)
        //        {
        //            case BlendOps.APNGBlendOpSource:
        //                foregroundBitmap.draw(
        //                    new Rect(fcTlChunk.XOffset, fcTlChunk.YOffset, fcTlChunk.Width, fcTlChunk.Height),
        //                    frameBitmap,
        //                    new Rect(0, 0, frameBitmap.PixelWidth, frameBitmap.PixelHeight),
        //                    WriteableBitmapExtensions.BlendMode.None);
        //                break;

        //            case BlendOps.APNGBlendOpOver:
        //                foregroundBitmap.Blit(
        //                    new Rect(fcTlChunk.XOffset, fcTlChunk.YOffset, fcTlChunk.Width, fcTlChunk.Height),
        //                    frameBitmap,
        //                    new Rect(0, 0, frameBitmap.PixelWidth, frameBitmap.PixelHeight),
        //                    WriteableBitmapExtensions.BlendMode.Alpha);
        //                break;
        //            default:
        //                throw new ArgumentOutOfRangeException();
        //        }

        //    }

        //    // dispose_op
        //    switch (fcTlChunk.DisposeOp)
        //    {
        //        case DisposeOps.APNGDisposeOpNone:
        //            backgroundBitmap = foregroundBitmap.Clone();
        //            break;
        //        case DisposeOps.APNGDisposeOpBackground:
        //            backgroundBitmap = null;
        //            break;
        //        case DisposeOps.APNGDisposeOpPrevious:
        //            backgroundBitmap = backgroundBitmap?.Clone();
        //            break;
        //        default:
        //            throw new ArgumentOutOfRangeException();
        //    }

        //}

        ~ApngBackgroundWorker()
        {
            DoDispose();
        }
    }
}