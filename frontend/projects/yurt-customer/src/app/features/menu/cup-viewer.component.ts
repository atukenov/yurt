import {
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  SimpleChanges,
} from '@angular/core';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

@Component({
  selector: 'cup-viewer',
  standalone: true,
  template: '',
  styles: [':host { display:block; width:260px; height:280px; }'],
})
export class CupViewerComponent implements OnInit, OnChanges, OnDestroy {
  /** URL to the .glb file */
  @Input() src = 'cup.glb';
  /** Target hex color for the coffee surface material */
  @Input() tint = '#c9a06e';
  /** How far the cup sways left/right from center, in degrees */
  @Input() swayDegrees = 40;
  /** Seconds for one full sway cycle (center → right → center → left → center) */
  @Input() swaySeconds = 4.5;
  /** Y rotation offset in degrees so the logo faces camera at the center of the sway */
  @Input() offset = -60;

  private renderer!: THREE.WebGLRenderer;
  private scene!: THREE.Scene;
  private camera!: THREE.PerspectiveCamera;
  private pivot!: THREE.Group;
  private coffeeMat: THREE.MeshStandardMaterial | THREE.MeshBasicMaterial | null = null;
  private tgtColor = new THREE.Color('#c9a06e');
  private raf = 0;
  private startTime = 0;
  private destroyed = false;

  constructor(private host: ElementRef<HTMLElement>) {}

  ngOnInit(): void {
    this.init();
  }

  ngOnChanges(ch: SimpleChanges): void {
    if (ch['tint'] && this.coffeeMat) {
      this.tgtColor.set(this.tint);
    }
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    cancelAnimationFrame(this.raf);
    this.renderer?.dispose();
    document.removeEventListener('visibilitychange', this.onVisibility);
  }

  private onVisibility = () => {
    if (document.hidden) {
      cancelAnimationFrame(this.raf);
    } else {
      this.raf = requestAnimationFrame(this.tick);
    }
  };

  private async init(): Promise<void> {
    const el = this.host.nativeElement;
    const w = el.clientWidth || 260;
    const h = el.clientHeight || 280;

    // Renderer
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    renderer.setSize(w, h);
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.domElement.style.cssText = 'width:100%;height:100%;display:block';
    el.appendChild(renderer.domElement);
    this.renderer = renderer;

    // Scene
    const scene = new THREE.Scene();
    this.scene = scene;

    scene.add(new THREE.AmbientLight(0xffffff, 1.1));
    const key = new THREE.DirectionalLight(0xffffff, 2.2);
    key.position.set(3, 5, 4);
    scene.add(key);
    const rim = new THREE.DirectionalLight(0xfff3c0, 1.0);
    rim.position.set(-4, 2, -3);
    scene.add(rim);

    // Camera (provisional — repositioned after model loads)
    const cam = new THREE.PerspectiveCamera(32, w / h, 0.01, 100);
    this.camera = cam;

    // Load model
    const glb = await new GLTFLoader().loadAsync(this.src);
    if (this.destroyed) return;

    const obj = glb.scene;
    const box = new THREE.Box3().setFromObject(obj);
    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());
    obj.position.sub(center);

    const pivot = new THREE.Group();
    pivot.add(obj);
    scene.add(pivot);
    this.pivot = pivot;

    const R = Math.max(size.x, size.y, size.z);
    cam.position.set(0, R * 0.85, R * 1.9);
    cam.lookAt(0, 0, 0);

    // Find coffee material
    obj.traverse((node) => {
      if (node instanceof THREE.Mesh && node.material) {
        const mats = Array.isArray(node.material) ? node.material : [node.material];
        for (const m of mats) {
          if (/coffee|tea/i.test((m as THREE.Material).name ?? '')) {
            this.coffeeMat = m as THREE.MeshStandardMaterial;
            break;
          }
        }
      }
    });

    this.tgtColor.set(this.tint);
    if (this.coffeeMat) this.coffeeMat.color.copy(this.tgtColor);

    pivot.rotation.y = (this.offset * Math.PI) / 180;

    this.startTime = performance.now();
    this.raf = requestAnimationFrame(this.tick);

    document.addEventListener('visibilitychange', this.onVisibility);
  }

  private tick = (t: number) => {
    if (this.destroyed) return;
    // Sways center → right → center → left → center rather than spinning all the
    // way around, so the logo stays roughly camera-facing at all times.
    const elapsed = (t - this.startTime) / 1000;
    const offsetRad = (this.offset * Math.PI) / 180;
    const amplitudeRad = (this.swayDegrees * Math.PI) / 180;
    const angularFreq = (2 * Math.PI) / this.swaySeconds;
    this.pivot.rotation.y = offsetRad + amplitudeRad * Math.sin(elapsed * angularFreq);
    if (this.coffeeMat) this.coffeeMat.color.lerp(this.tgtColor, 0.08);
    this.renderer.render(this.scene, this.camera);
    this.raf = requestAnimationFrame(this.tick);
  };
}
