export class LandingPage {
    constructor() {
        this.autoScrollIntervals = [];
        this.isDragging = false;
        this.startX = 0;
        this.scrollLeft = 0;
        this.userInteracting = false;
    }

    initialize() {
        console.log('✅ LandingPage.js initialized');
        
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.setupScrolls());
        } else {
            this.setupScrolls();
        }
    }

    setupScrolls() {
        const featuredWrapper = document.querySelector('.featured-section .scroll-wrapper');
        const testimonialsWrapper = document.querySelector('.testimonials-section .testimonials-scroll-wrapper');

        if (featuredWrapper) {
            this.setupInfiniteAutoScroll(featuredWrapper, '.scroll-content', 1);
        }

        if (testimonialsWrapper) {
            this.setupInfiniteAutoScroll(testimonialsWrapper, '.testimonials-scroll-content', 1);
        }
    }

    setupInfiniteAutoScroll(wrapper, contentSelector, scrollSpeed = 1) {
        const content = wrapper.querySelector(contentSelector);
        if (!content) return;

        let animationId = null;
        let currentPosition = 0;
        let isPaused = false;

        const autoScroll = () => {
            if (!isPaused && !this.userInteracting) {
                currentPosition += scrollSpeed;
                
                const maxScroll = content.scrollWidth / 2;
                
                if (currentPosition >= maxScroll) {
                    currentPosition = 0;
                }
                
                wrapper.scrollLeft = currentPosition;
            }
            
            animationId = requestAnimationFrame(autoScroll);
        };

        // Start auto-scroll
        animationId = requestAnimationFrame(autoScroll);

        // Pause on hover
        wrapper.addEventListener('mouseenter', () => {
            isPaused = true;
        });

        wrapper.addEventListener('mouseleave', () => {
            isPaused = false;
        });

        // Pause on touch/drag
        wrapper.addEventListener('touchstart', () => {
            this.userInteracting = true;
            isPaused = true;
        });

        wrapper.addEventListener('touchend', () => {
            this.userInteracting = false;
            setTimeout(() => {
                isPaused = false;
            }, 2000);
        });

        // Mouse drag
        wrapper.addEventListener('mousedown', (e) => {
            this.isDragging = true;
            this.userInteracting = true;
            isPaused = true;
            this.startX = e.pageX - wrapper.offsetLeft;
            this.scrollLeft = wrapper.scrollLeft;
            wrapper.style.cursor = 'grabbing';
        });

        wrapper.addEventListener('mouseleave', () => {
            if (this.isDragging) {
                this.isDragging = false;
                this.userInteracting = false;
                wrapper.style.cursor = 'grab';
                setTimeout(() => {
                    isPaused = false;
                }, 2000);
            }
        });

        wrapper.addEventListener('mouseup', () => {
            if (this.isDragging) {
                this.isDragging = false;
                this.userInteracting = false;
                wrapper.style.cursor = 'grab';
                setTimeout(() => {
                    isPaused = false;
                }, 2000);
            }
        });

        wrapper.addEventListener('mousemove', (e) => {
            if (!this.isDragging) return;
            e.preventDefault();
            const x = e.pageX - wrapper.offsetLeft;
            const walk = (x - this.startX) * 2;
            currentPosition = this.scrollLeft - walk;
            wrapper.scrollLeft = currentPosition;
        });

        // Manual scroll detection
        let scrollTimeout;
        wrapper.addEventListener('scroll', () => {
            if (!this.isDragging && !this.userInteracting) {
                this.userInteracting = true;
                isPaused = true;
                
                clearTimeout(scrollTimeout);
                scrollTimeout = setTimeout(() => {
                    this.userInteracting = false;
                    isPaused = false;
                }, 2000);
            }
        });

        // Store animation ID for cleanup
        this.autoScrollIntervals.push(() => {
            if (animationId) {
                cancelAnimationFrame(animationId);
            }
        });
    }

    destroy() {
        this.autoScrollIntervals.forEach(cleanup => cleanup());
        this.autoScrollIntervals = [];
    }
}

// Auto-initialize
if (typeof window !== 'undefined') {
    window.LandingPage = LandingPage;
    
    const landingPageInstance = new LandingPage();
    landingPageInstance.initialize();
    
    if (window.Blazor) {
        window.Blazor.addEventListener('enhancedload', () => {
            landingPageInstance.destroy();
            landingPageInstance.initialize();
        });
    }
}


