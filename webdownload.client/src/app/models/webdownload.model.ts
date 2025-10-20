export class downloadInfo {
  speed: string; //ReceiveSpeed
  eta: string; //ReceiveETA
  size: string; //ReceiveTotalSize
  frag: string; // ReceiveTotalSize
  state: string;
  output: string;
  progress: string;
  fileName: string;
  chapter: string;
  error: string;
  finishOutput: string;
  constructor(speed: string, eta: string, size: string, frag: string, state: string, output: string, progress: string, fileName: string, chapter: string, error: string, finishOutput: string) {
    this.speed = speed;
    this.eta = eta;
    this.size = size;
    this.frag = frag;
    this.state = state;
    this.output = output;
    this.progress = progress;
    this.fileName = fileName;
    this.chapter = chapter;
    this.finishOutput = finishOutput;
    this.error = error;

  }
}
