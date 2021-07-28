using Microsoft.MixedReality.WebRTC;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace WebRTCEX02
{
    public delegate void RoomJoin(string roomId);
    public delegate void GetSdp(SdpMessage message);
    public delegate void GetCandidate(IceCandidate iceCandidate);

    public sealed partial class MainPage : Page
    {
        private Signaler signaler = null;
        private LocalMedia localMedia = null;
        private RemoteMedia remoteMedia = null;

        public MainPage()
        {
            this.signaler = new Signaler();
            this.signaler.RoomJoinEvent += onRoomJoin;

            this.InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) {
            this.localMedia = new LocalMedia(localVideoPlayerElement);
            this.remoteMedia = new RemoteMedia(remoteVideoPlayerElement);

            this.signaler.SetMedia(this.localMedia, this.remoteMedia);
        }

        private async void start(object sender, RoutedEventArgs e) {
            localMedia.startMedia();
        }

        private async void call(object sender, RoutedEventArgs e) {
            if (roomIdEl.Text.Trim() != "") {
                signaler.call(roomIdEl.Text);
            } else {
                signaler.roomJoin();
            }
        }

        public void onRoomJoin(string roomId) {
            roomIdEl.Text = roomId;
            signaler.call(roomId);
        }

        private async void hangUp(object sender, RoutedEventArgs e) {
            signaler.dispose();
        }
    }
}
