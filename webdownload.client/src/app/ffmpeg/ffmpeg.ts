import { Component } from '@angular/core';
import { SafeUrlPipe } from '../pipes/safe-url-pipe';
import { CommonModule } from '@angular/common';
@Component({
  selector: 'app-ffmpeg',
  imports: [CommonModule, SafeUrlPipe],
  templateUrl: './ffmpeg.html',
  styleUrl: './ffmpeg.scss',
})
export class Ffmpeg {

}
