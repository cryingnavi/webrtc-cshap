using Microsoft.MixedReality.WebRTC;
using System;
using System.Collections.Generic;

namespace WebRTCEX02 {
    class Peer {
        private PeerConnection _peerConnection = null;
        LocalMedia localMedia = null;
        RemoteMedia remoteMedia = null;

        public event GetSdp GetSdpEvent;
        public event GetCandidate GetCandidateEvent;


        public Peer(LocalMedia localMedia, RemoteMedia remoteMedia) {
            this.localMedia = localMedia;
            this.remoteMedia = remoteMedia;
            this.createPeer();
        }

        public async void createPeer() {
            this._peerConnection = new PeerConnection();

            this._peerConnection.LocalSdpReadytoSend += Peer_LocalSdpReadytoSend;
            this._peerConnection.IceCandidateReadytoSend += Peer_IceCandidateReadytoSend;

            this._peerConnection.Connected += () => {
                
            };
            this._peerConnection.IceStateChanged += (IceConnectionState newState) => {
                
            };

            this._peerConnection.VideoTrackAdded += (RemoteVideoTrack track) => {
                Console.WriteLine(track);
                this.remoteMedia.startMedia(track);
            };

            var config = new PeerConnectionConfiguration {
                IceServers = new List<IceServer> {
                    new IceServer{
                        Urls = { "stun:stun.l.google.com:19302" }
                    },
                    new IceServer{
                      Urls = { "turn:192.158.29.39:3478?transport=udp" },
                      TurnPassword = "JZEOEt2V3Qb0y27GRntt2u2PAYA=",
                      TurnUserName = "28224511:1379330808"
                    },
                    new IceServer{
                        Urls = { "turn:192.158.29.39:3478?transport=tcp" },
                        TurnPassword = "JZEOEt2V3Qb0y27GRntt2u2PAYA=",
                        TurnUserName = "28224511:1379330808"
                    }
                }
            };
            await this._peerConnection.InitializeAsync(config);
        }

        public Transceiver AddTransceiver(MediaKind mediaKind, TransceiverInitSettings settings = null) {
            return this._peerConnection.AddTransceiver(mediaKind);
        }

        private void Peer_LocalSdpReadytoSend(SdpMessage message) {
            Console.WriteLine(message);
            GetSdpEvent(message);
        }

        private void Peer_IceCandidateReadytoSend(IceCandidate iceCandidate) {
            //var msg = NodeDssSignaler.FromIceCandidate(iceCandidate);
            //_signaler.SendMessageAsync(msg);
            Console.WriteLine(iceCandidate);
            GetCandidateEvent(iceCandidate);
        }

        public void SetOfferSDP(string content) {
            var message = new SdpMessage { Type = SdpMessageType.Offer, Content = content };

            this._peerConnection.SetRemoteDescriptionAsync(message);
        }

        public void SetAnswerSDP(string content) {
            var message = new SdpMessage { Type = SdpMessageType.Answer, Content = content };

            this._peerConnection.SetRemoteDescriptionAsync(message);
        }

        public void CreateAnswer() {
            this._peerConnection.CreateAnswer();
        }

        public void CreateOffer() {
            this._peerConnection.CreateOffer();
        }

        public void AddIceCandidate(string content, string sdpMid, int sdpMLineIndex) {
            var message = new IceCandidate {
                SdpMid = sdpMid,
                SdpMlineIndex = sdpMLineIndex,
                Content = content
            };
            this._peerConnection.AddIceCandidate(message);
        }

        public void dispose() {
            if (this._peerConnection != null) {
                this._peerConnection.Close();
                this._peerConnection.Dispose();
                this._peerConnection = null;
            }
        }
    }
}

