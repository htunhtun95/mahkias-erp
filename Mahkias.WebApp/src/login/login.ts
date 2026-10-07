import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MaterialModule } from '../app/material-module';
import { GoogleAuthService } from '../auth/google-auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, RouterModule, MaterialModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class LoginComponent implements OnInit {
  loading = true;

  constructor(
    private googleAuth: GoogleAuthService,
    private route: ActivatedRoute,
    private router: Router,
  ) {}

  ngOnInit(): void {
    if (this.googleAuth.user) {
      this.redirectToReturnUrl();
      return;
    }

    this.googleAuth.user$.subscribe((user) => {
      if (user) {
        this.redirectToReturnUrl();
      }
    });

    this.loading = false;

    setTimeout(() => {
      this.googleAuth.renderButton('google-button');
    }, 200);
  }

  private redirectToReturnUrl() {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') || '/dashboard';
    this.router.navigateByUrl(returnUrl);
  }
}
