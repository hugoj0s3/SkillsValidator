// <voice-panel session-id="..." agent-name="..."> is the voice of the interview.
// It captures the microphone, talks to the server over one WebSocket and plays the agent's voice.
// Protocol (see InterviewVoiceEndpoint.cs):
//   send     binary  microphone audio, 16-bit mono PCM at 16 kHz (made by pcm-worklet.js)
//   receive  binary  agent voice, 16-bit mono PCM at 24 kHz
//   receive  text    {"type":"caption","text"} | {"type":"stop"} | {"type":"ended"} | {"type":"error","message"}
const AGENT_SAMPLE_RATE = 24000; // must match AudioChunk.AgentSampleRate

class VoicePanel extends HTMLElement {
    connectedCallback() {
        this.innerHTML = `
            <button class="primary">Start talking</button>
            <p class="status hint">Use headphones for best results. You can interrupt the agent at any time.</p>
            <p class="caption"></p>
            <p class="voice-error"></p>`;

        this.button = this.querySelector("button");
        this.statusLine = this.querySelector(".status");
        this.captionLine = this.querySelector(".caption");
        this.errorLine = this.querySelector(".voice-error");
        this.button.addEventListener("click", () => this.start());

        this.playingNodes = new Set();
        this.playbackEnd = 0; // AudioContext time when the queued agent audio ends
    }

    // The element left the page ("End interview" or navigation): release everything.
    disconnectedCallback() {
        this.stop();
    }

    // The browser only allows the microphone and audio playback after a click.
    async start() {
        this.button.disabled = true;
        this.showError("");

        try {
            this.micStream = await navigator.mediaDevices.getUserMedia({
                audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true }
            });
        } catch (error) {
            this.showError(microphoneErrorMessage(error));
            this.button.disabled = false;
            return;
        }

        this.context = new AudioContext();
        await this.context.audioWorklet.addModule("js/pcm-worklet.js");

        const protocol = location.protocol === "https:" ? "wss:" : "ws:";
        const sessionId = this.getAttribute("session-id");
        this.socket = new WebSocket(`${protocol}//${location.host}/interview/${sessionId}/voice`);
        this.socket.binaryType = "arraybuffer";
        this.socket.onopen = () => this.startMicrophone();
        this.socket.onmessage = event => this.onMessage(event.data);
        this.socket.onclose = () => this.onConnectionLost();

        this.button.hidden = true;
        this.setStatus("Connecting…", "");
    }

    // Microphone -> pcm-worklet.js (16 kHz PCM, 100 ms chunks) -> WebSocket.
    startMicrophone() {
        this.micSource = this.context.createMediaStreamSource(this.micStream);
        this.captureNode = new AudioWorkletNode(this.context, "pcm-capture");
        this.captureNode.port.onmessage = event => {
            if (this.socket?.readyState === WebSocket.OPEN) {
                this.socket.send(event.data);
            }
        };

        // The worklet must be connected to the output to keep running; a muted gain avoids hearing yourself.
        const mute = this.context.createGain();
        mute.gain.value = 0;
        this.micSource.connect(this.captureNode).connect(mute).connect(this.context.destination);

        this.setStatus("Listening…", "listening");
    }

    onMessage(data) {
        if (data instanceof ArrayBuffer) {
            this.play(data);
            return;
        }

        const message = JSON.parse(data);
        switch (message.type) {
            case "caption":
                this.captionLine.textContent = message.text ? `“${message.text}”` : "";
                break;
            case "stop":
                this.stopPlayback();
                break;
            case "ended":
                this.onEnded();
                break;
            case "error":
                this.stop();
                this.showError(message.message);
                this.reset();
                break;
        }
    }

    // The agent said goodbye: let it finish speaking, then show the result.
    async onEnded() {
        const remainingSeconds = Math.max(0, this.playbackEnd - this.context.currentTime);
        await new Promise(resolve => setTimeout(resolve, remainingSeconds * 1000));
        this.stop();
        location.assign(`/result/${this.getAttribute("session-id")}`);
    }

    // The socket closed without the server saying why (network problem, server restart...).
    onConnectionLost() {
        this.stop();
        this.showError("The voice connection was lost.");
        this.reset();
    }

    // Plays one sentence of agent audio right after the audio already queued (gapless).
    play(pcm) {
        const samples = new Int16Array(pcm);
        const buffer = this.context.createBuffer(1, samples.length, AGENT_SAMPLE_RATE);
        const channel = buffer.getChannelData(0);
        for (let i = 0; i < samples.length; i++) {
            channel[i] = samples[i] / 32768;
        }

        const node = this.context.createBufferSource();
        node.buffer = buffer;
        node.connect(this.context.destination);

        const startAt = Math.max(this.context.currentTime, this.playbackEnd);
        node.start(startAt);
        this.playbackEnd = startAt + buffer.duration;

        this.playingNodes.add(node);
        this.setStatus(`${this.getAttribute("agent-name")} is speaking…`, "speaking");
        node.onended = () => {
            this.playingNodes.delete(node);
            if (this.playingNodes.size === 0) {
                this.setStatus("Listening…", "listening");
            }
        };
    }

    // The participant interrupted: silence the agent immediately.
    stopPlayback() {
        for (const node of this.playingNodes) {
            node.onended = null;
            node.stop();
        }
        this.playingNodes.clear();
        this.playbackEnd = 0;
        this.setStatus("Listening…", "listening");
    }

    // Closes the socket (the server then stops the interview loop) and releases the microphone.
    stop() {
        if (this.socket) {
            this.socket.onclose = null;
            this.socket.close();
            this.socket = null;
        }

        if (this.context) {
            this.stopPlayback();
            this.micSource?.disconnect();
            this.captureNode?.disconnect();
            this.context.close();
            this.context = null;
        }

        this.micStream?.getTracks().forEach(track => track.stop());
        this.micStream = null;
    }

    // Shows "Start talking" again, e.g. after an error.
    reset() {
        this.button.hidden = false;
        this.button.disabled = false;
        this.captionLine.textContent = "";
        this.setStatus("", "");
    }

    setStatus(text, cssClass) {
        this.statusLine.textContent = text;
        this.statusLine.className = `status ${cssClass}`;
    }

    showError(text) {
        this.errorLine.textContent = text;
    }
}

function microphoneErrorMessage(error) {
    switch (error.name) {
        case "NotAllowedError":
            return "Microphone access was denied. Allow the microphone in the browser's site settings and try again.";
        case "NotFoundError":
            return "No microphone was found. Connect or enable one (Windows: Settings > System > Sound > Input) and try again.";
        case "NotReadableError":
            return "The microphone could not be started. It may be in use by another app or blocked by Windows privacy settings.";
        default:
            return `Could not access the microphone (${error.name}: ${error.message}).`;
    }
}

customElements.define("voice-panel", VoicePanel);
