import lottie from 'lottie-web/build/player/lottie_light.js'
import confetti from '@/assets/animations/auction-confetti.json'

export function loadConfetti(container: HTMLDivElement) {
  return lottie.loadAnimation({
    container,
    renderer: 'svg',
    loop: false,
    autoplay: true,
    animationData: structuredClone(confetti),
    rendererSettings: { preserveAspectRatio: 'xMidYMid slice' },
  })
}
