import { Component} from '@angular/core';
import { CommonModule } from '@angular/common';
import { SafeUrlPipe } from '../pipes/safe-url-pipe';
@Component({
  selector: 'app-ffmpeg',
  imports: [CommonModule, SafeUrlPipe],
  templateUrl: './ffmpeg.html',
  styleUrls: ['./ffmpeg.scss']
})
export class Ffmpeg  {
}
