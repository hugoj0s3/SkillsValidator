// Runs on the audio thread: converts the microphone input (usually 48 kHz float)
// to 16 kHz 16-bit PCM and posts it in ~100 ms chunks.
const TARGET_SAMPLE_RATE = 16000; // must match AudioChunk.MicrophoneSampleRate
const CHUNK_SAMPLES = 1600; // 100 ms at 16 kHz

class PcmCaptureProcessor extends AudioWorkletProcessor {
    constructor() {
        super();
        this.ratio = sampleRate / TARGET_SAMPLE_RATE;
        this.position = 0;
        this.sum = 0;
        this.count = 0;
        this.output = new Int16Array(CHUNK_SAMPLES);
        this.outputIndex = 0;
    }

    process(inputs) {
        const input = inputs[0] && inputs[0][0];
        if (!input) {
            return true;
        }

        for (let i = 0; i < input.length; i++) {
            // Average the input samples that fall into one output sample (simple low-pass + downsample).
            this.sum += input[i];
            this.count++;
            this.position++;

            if (this.position >= this.ratio) {
                this.position -= this.ratio;
                const sample = Math.max(-1, Math.min(1, this.sum / this.count));
                this.output[this.outputIndex++] = sample < 0 ? sample * 0x8000 : sample * 0x7fff;
                this.sum = 0;
                this.count = 0;

                if (this.outputIndex === CHUNK_SAMPLES) {
                    this.port.postMessage(this.output.buffer, [this.output.buffer]);
                    this.output = new Int16Array(CHUNK_SAMPLES);
                    this.outputIndex = 0;
                }
            }
        }

        return true;
    }
}

registerProcessor("pcm-capture", PcmCaptureProcessor);
