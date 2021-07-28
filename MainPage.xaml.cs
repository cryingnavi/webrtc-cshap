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
            this.InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e) {
            this.signaler = new Signaler();
            this.signaler.RoomJoinEvent += onRoomJoin;

            //각각 UI 엘리먼트인 localVideoPlayerElement, remoteVideoPlayerElement를 넘겨줍니다. 
            //각 엘리먼트는 적절한 시점에 클래스 안에서 비디오와 바인딩됩니다.
            this.localMedia = new LocalMedia(localVideoPlayerElement);
            this.remoteMedia = new RemoteMedia(remoteVideoPlayerElement);

            this.signaler.SetMedia(this.localMedia, this.remoteMedia);
        }
        public void onRoomJoin(string roomId) {
            roomIdEl.Text = roomId;
            signaler.call(roomId);
        }

        //Start 버튼 이벤트
        private async void start(object sender, RoutedEventArgs e) {
            localMedia.startMedia();
        }

        //Call 버튼 이벤트
        private async void call(object sender, RoutedEventArgs e) {
            if (roomIdEl.Text.Trim() != "") {
                signaler.call(roomIdEl.Text);
            } else {
                signaler.roomJoin();
            }
        }

        ///HangUp 버튼 이벤트
        private async void hangUp(object sender, RoutedEventArgs e) {
            signaler.dispose();
        }
    }
}
