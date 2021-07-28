using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TestAppUwp.Video;
using Microsoft.MixedReality.WebRTC;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;
using Windows.Media.MediaProperties;
using Windows.UI.Core;
using Windows.ApplicationModel.Core;

namespace WebRTCEX02 {
	class RemoteMedia {
        private MediaPlayerElement remoteVideoPlayerElement = null;

        private MediaStreamSource _remoteVideoSource;
        private VideoBridge _remoteVideoBridge = new VideoBridge(3);
        private bool _remoteVideoPlaying = false;
        private object _remoteVideoLock = new object();

        public RemoteVideoTrack remoteVideoTrack = null;
        public LocalAudioTrack remoteAudioTrack = null;

        public RemoteMedia(MediaPlayerElement remoteVideoPlayerElement) {
            this.remoteVideoPlayerElement = remoteVideoPlayerElement;
        }

        public async void startMedia(RemoteVideoTrack track) {
            remoteVideoTrack = track;
            remoteVideoTrack.I420AVideoFrameReady += RemoteVideo_I420AFrameReady;
        }

        private void RemoteVideo_I420AFrameReady(I420AVideoFrame frame) {
            lock (_remoteVideoLock) {
                if (!_remoteVideoPlaying) {
                    _remoteVideoPlaying = true;

                    uint width = frame.width;
                    uint height = frame.height;

                    RunOnMainThread(() => {
                        int framerate = 30;
                        _remoteVideoSource = CreateI420VideoStreamSource(width, height, framerate);

                        var remoteVideoPlayer = new MediaPlayer();
                        remoteVideoPlayer.Source = MediaSource.CreateFromMediaStreamSource(_remoteVideoSource);

                        this.remoteVideoPlayerElement.SetMediaPlayer(remoteVideoPlayer);
                        remoteVideoPlayer.Play();
                    });
                }
            }
            _remoteVideoBridge.HandleIncomingVideoFrame(frame);
        }

        private void RunOnMainThread(Windows.UI.Core.DispatchedHandler handler) {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                handler.Invoke();
            });
        }

        private MediaStreamSource CreateI420VideoStreamSource(uint width, uint height, int framerate) {
            if (width == 0) {
                throw new ArgumentException("Invalid zero width for video.", "width");
            }
            if (height == 0) {
                throw new ArgumentException("Invalid zero height for video.", "height");
            }

            var videoProperties = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Iyuv, width, height);
            var videoStreamDesc = new VideoStreamDescriptor(videoProperties);
            videoStreamDesc.EncodingProperties.FrameRate.Numerator = (uint)framerate;
            videoStreamDesc.EncodingProperties.FrameRate.Denominator = 1;
            videoStreamDesc.EncodingProperties.Bitrate = ((uint)framerate * width * height * 12);
            
            var videoStreamSource = new MediaStreamSource(videoStreamDesc);
            videoStreamSource.BufferTime = TimeSpan.Zero;
            videoStreamSource.SampleRequested += OnMediaStreamSourceRequested;
            videoStreamSource.IsLive = true;
            videoStreamSource.CanSeek = false;
            
            return videoStreamSource;
        }

        private void OnMediaStreamSourceRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args) {
            VideoBridge videoBridge;
            if (sender == _remoteVideoSource)
                videoBridge = _remoteVideoBridge;
            else
                return;
            videoBridge.TryServeVideoFrame(args);
        }

        public void dispose() {
            remoteVideoPlayerElement.SetMediaPlayer(null);
        }
    }
}
