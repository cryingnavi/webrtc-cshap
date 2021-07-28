using Microsoft.MixedReality.WebRTC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebRTCEX02.Message.Sdp;
using WebRTCEX02.Message.Candidate;

namespace WebRTCEX02 {
	class Signaler {
		string HttpServerAddress = "https://test.studio-level9999.com";
		Uri serverUri = new Uri("wss://test.studio-level9999.com");

		HttpClient client = null;
		ClientWebSocket ws = null;

		string token = null;

		Peer peer = null;

		LocalMedia localMedia = null;
		RemoteMedia remoteMedia = null;

		String roomId = "";
		String type = "offer";

		public event RoomJoin RoomJoinEvent;

		public Signaler() {
			this.init();
		}

		public async void init() {
			client = new HttpClient();
			ws = new ClientWebSocket();
			await ws.ConnectAsync(serverUri, CancellationToken.None);

			while (ws.State == WebSocketState.Open) {
				int bufferSize = 1024;
				var buffer = new byte[bufferSize];
				var offset = 0;
				var free = buffer.Length;

				//모든 메시지를 받을때까지 반복한다.
				while (true) {
					ArraySegment<byte> bytesReceived = new ArraySegment<byte>(buffer, offset, free);
					WebSocketReceiveResult result = await ws.ReceiveAsync(bytesReceived, CancellationToken.None);
					offset += result.Count;
					free -= result.Count;
					if (result.EndOfMessage) break;
					if (free == 0) {
						var newSize = buffer.Length + bufferSize;
						var newBuffer = new byte[newSize];
						Array.Copy(buffer, 0, newBuffer, 0, offset);
						buffer = newBuffer;
						free = buffer.Length - offset;
					}
				}

				//받은 메시지를 JSON 객체로 변환한다.
				dynamic data = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(buffer.ToArray(), 0, offset));
				if (data != null) {
					string command = data.header.command;
					if (command == "connect") {
						token = data.body.token;
					} else if (command == "on_call_offer") {
						this.type = "offer";

						Thread t1 = new Thread(delegate () {
							this.CreatePeer();
							peer.CreateOffer();
						});
						t1.Start();
					} else if (command == "on_call_answer") {
						this.type = "answer";

						Thread t1 = new Thread(delegate () {
							this.CreatePeer();
						});
						t1.Start();
					} else if (command == "on_offer_sdp") {
						var sdp = data.body.sdp;
						var json = JsonConvert.DeserializeObject(sdp.Value);
						var _sdp = json.sdp.Value;
					
						Thread t1 = new Thread(delegate () {
							peer.SetOfferSDP(_sdp);
							peer.CreateAnswer();
						});
						t1.Start();
					} else if (command == "on_answer_sdp") {
						var sdp = data.body.sdp;
						var json = JsonConvert.DeserializeObject(sdp.Value);
						var _sdp = json.sdp.Value;

						Thread t1 = new Thread(delegate () {
							peer.SetAnswerSDP(_sdp);
						});
						t1.Start();
					} else if (command == "on_offer_candidate") {
						var candidate = data.body.candidate;
						var json = JsonConvert.DeserializeObject(candidate.Value);
						string _candidate = json.candidate.Value;
						string sdpMid = json.sdpMid.Value;
						int sdpMLineIndex = (int)json.sdpMLineIndex.Value;

						Thread t1 = new Thread(delegate () {
							peer.AddIceCandidate(_candidate, sdpMid, sdpMLineIndex);
						});
						t1.Start();
					} else if (command == "on_answer_candidate") {
						var candidate = data.body.candidate;
						var json = JsonConvert.DeserializeObject(candidate.Value);
						string _candidate = json.candidate.Value;
						string sdpMid = json.sdpMid.Value;
						int sdpMLineIndex = (int)json.sdpMLineIndex.Value;

						Thread t1 = new Thread(delegate () {
							peer.AddIceCandidate(_candidate, sdpMid, sdpMLineIndex);
						});
						t1.Start();
					}	
				}
			}
		}

		private void CreatePeer()  {
			Transceiver videoTransveiver = peer.AddTransceiver(MediaKind.Video);
			videoTransveiver.LocalVideoTrack = localMedia.localVideoTrack;
			videoTransveiver.DesiredDirection = Transceiver.Direction.SendReceive;

			Transceiver audioTransveiver = peer.AddTransceiver(MediaKind.Audio);
			audioTransveiver.LocalAudioTrack = localMedia.localAudioTrack;
			audioTransveiver.DesiredDirection = Transceiver.Direction.SendReceive;
		}

		public void SetMedia(LocalMedia localMedia, RemoteMedia remoteMedia) {
			this.localMedia = localMedia;
			this.remoteMedia = remoteMedia;
			this.peer = new Peer(this.localMedia, this.remoteMedia);

			this.peer.GetSdpEvent += OnGetSdp;
			this.peer.GetCandidateEvent += OnGetCandidate;			
		}

		public void OnGetSdp(SdpMessage message) {
			if (this.type == "offer") {
				SendOfferSdp(message.Content);
			} else {
				SendAnswerSdp(message.Content);
			}
		}

		public void OnGetCandidate(IceCandidate iceCandidate) {
			if (this.type == "offer") {
				this.SendOfferCandidate(iceCandidate);
			} else {
				this.SendAnswerCandidate(iceCandidate);
			}
		}

		public async void roomJoin() {
			var response = await client.GetAsync(HttpServerAddress + "/roomReady");

			string result = response.Content.ReadAsStringAsync().Result;
			dynamic data = JObject.Parse(result);

			RoomJoinEvent(data.body.roomId.ToString());
		}

		public async void call(string roomId) {
			this.roomId = roomId;
			string json = "{\"header\":{\"command\":\"call\",\"token\":\"" + token + "\"},\"body\":{\"roomId\":" + roomId + "}}";
			ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
		}

		public async void SendOfferSdp(string sdp) {
			SdpHeaderJson hMsg = new SdpHeaderJson();
			hMsg.command = "offer_sdp";
			hMsg.token = token;

			SdpDescJson sdpDescJson = new SdpDescJson();
			sdpDescJson.type = "offer";
			sdpDescJson.sdp = sdp;

			SdpBodyJson sdbBody = new SdpBodyJson();
			sdbBody.roomId = this.roomId;
			sdbBody.sdp = JsonConvert.SerializeObject(sdpDescJson, Formatting.None);

			SdpJson sdpMsg = new SdpJson();
			sdpMsg.header = hMsg;
			sdpMsg.body = sdbBody;

			var json = JsonConvert.SerializeObject(sdpMsg, Formatting.None);

			ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
		}

		public async void SendAnswerSdp(string sdp) {
			SdpHeaderJson hMsg = new SdpHeaderJson();
			hMsg.command = "answer_sdp";
			hMsg.token = token;

			SdpDescJson sdpDescJson = new SdpDescJson();
			sdpDescJson.type = "answer";
			sdpDescJson.sdp = sdp;

			SdpBodyJson sdbBody = new SdpBodyJson();
			sdbBody.roomId = this.roomId;
			sdbBody.sdp = JsonConvert.SerializeObject(sdpDescJson, Formatting.None);

			SdpJson sdpMsg = new SdpJson();
			sdpMsg.header = hMsg;
			sdpMsg.body = sdbBody;

			var json = JsonConvert.SerializeObject(sdpMsg, Formatting.None);

			ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
		}
		public async void SendOfferCandidate(IceCandidate candidate) {
			CandidateHeaderJson hMsg = new CandidateHeaderJson();
			hMsg.command = "offer_candidate";
			hMsg.token = token;

			CandidateDescJson candidateDescJson = new CandidateDescJson();
			candidateDescJson.candidate = candidate.Content;
			candidateDescJson.sdpMid = candidate.SdpMid;
			candidateDescJson.sdpMLineIndex = candidate.SdpMlineIndex;

			CandidateBodyJson candidateBodyJson = new CandidateBodyJson();
			candidateBodyJson.roomId = this.roomId;
			candidateBodyJson.candidate = JsonConvert.SerializeObject(candidateDescJson, Formatting.None);

			CandidateJson candidateMsg = new CandidateJson();
			candidateMsg.header = hMsg;
			candidateMsg.body = candidateBodyJson;

			var json = JsonConvert.SerializeObject(candidateMsg, Formatting.None);

			ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
		}

		public async void SendAnswerCandidate(IceCandidate candidate) {
			CandidateHeaderJson hMsg = new CandidateHeaderJson();
			hMsg.command = "answer_candidate";
			hMsg.token = token;

			CandidateDescJson candidateDescJson = new CandidateDescJson();
			candidateDescJson.candidate = candidate.Content;
			candidateDescJson.sdpMid = candidate.SdpMid;
			candidateDescJson.sdpMLineIndex = candidate.SdpMlineIndex;

			CandidateBodyJson candidateBodyJson = new CandidateBodyJson();
			candidateBodyJson.roomId = this.roomId;
			candidateBodyJson.candidate = JsonConvert.SerializeObject(candidateDescJson, Formatting.None);

			CandidateJson candidateMsg = new CandidateJson();
			candidateMsg.header = hMsg;
			candidateMsg.body = candidateBodyJson;

			var json = JsonConvert.SerializeObject(candidateMsg, Formatting.None);

			ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
		}

		public void dispose() {
			this.localMedia.dispose();
			this.remoteMedia.dispose();

			this.ws.CloseAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
		}
	}
}
