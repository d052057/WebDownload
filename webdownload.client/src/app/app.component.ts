import { Component} from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  imports: [RouterOutlet, RouterLink, MatIconModule],
  styleUrl: './app.component.scss'
})
export class AppComponent {
}
