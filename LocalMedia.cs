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
	class LocalMedia {
        private MediaPlayerElement localVideoPlayerElement = null;

        private MediaStreamSource _localVideoSource;
        private VideoBridge _localVideoBridge = new VideoBridge(3);
        private bool _localVideoPlaying = false;
        private object _localVideoLock = new object();
        private DeviceAudioTrackSource _microphoneSource;
        private DeviceVideoTrackSource _webcamSource;

        public LocalVideoTrack localVideoTrack = null;
        public LocalAudioTrack localAudioTrack = null;

        public LocalMedia(MediaPlayerElement localVideoPlayerElement) {
            this.localVideoPlayerElement = localVideoPlayerElement;
        }

        public async void startMedia() {  
            LocalAudioTrack _localAudioTrack;
            LocalVideoTrack _localVideoTrack;

            _webcamSource = await DeviceVideoTrackSource.CreateAsync();
            _microphoneSource = await DeviceAudioTrackSource.CreateAsync();

            var videoTrackConfig = new LocalVideoTrackInitConfig {
                trackName = "webcam_track"
            };
            _localVideoTrack = LocalVideoTrack.CreateFromSource(_webcamSource, videoTrackConfig);

            var audioTrackConfig = new LocalAudioTrackInitConfig {
                trackName = "microphone_track"
            };
            _localAudioTrack = LocalAudioTrack.CreateFromSource(_microphoneSource, audioTrackConfig);

            _webcamSource.I420AVideoFrameReady += LocalI420AFrameReady;

            this.localVideoTrack = _localVideoTrack;
            this.localAudioTrack = _localAudioTrack;
        }

        private void LocalI420AFrameReady(I420AVideoFrame frame) {
            lock (_localVideoLock) {
                if (!_localVideoPlaying) {
                    _localVideoPlaying = true;

                    uint width = frame.width;
                    uint height = frame.height;

                    RunOnMainThread(() => {
                        int framerate = 30;
                        _localVideoSource = CreateI420VideoStreamSource(width, height, framerate);

                        var localVideoPlayer = new MediaPlayer();
                        localVideoPlayer.Source = MediaSource.CreateFromMediaStreamSource(_localVideoSource);

                        this.localVideoPlayerElement.SetMediaPlayer(localVideoPlayer);
                        localVideoPlayer.Play();
                    });
                }
            }
            _localVideoBridge.HandleIncomingVideoFrame(frame);
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
            if (sender == _localVideoSource)
                videoBridge = _localVideoBridge;
            else
                return;
            videoBridge.TryServeVideoFrame(args);
        }

        public void dispose() {
            localAudioTrack?.Dispose();
            localVideoTrack?.Dispose();
            _microphoneSource?.Dispose();
            _webcamSource?.Dispose();

            localVideoPlayerElement.SetMediaPlayer(null);
            _localVideoPlaying = false;
        }
    }
}