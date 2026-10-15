import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class SoundService {
  private audio: HTMLAudioElement | null = null;

  constructor() {
    const unlock = () => {
      this.audio = new Audio('/sound.wav');
      this.audio.load();
      document.removeEventListener('click', unlock, true);
      document.removeEventListener('keydown', unlock, true);
    };
    document.addEventListener('click', unlock, true);
    document.addEventListener('keydown', unlock, true);
  }

  async playNewOrder(): Promise<void> {
    try {
      if (!this.audio) this.audio = new Audio('/sound.wav');
      this.audio.currentTime = 0;
      await this.audio.play();
    } catch { }
  }
}
