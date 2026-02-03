// Pages/Main/LandingPage.razor.js
export class LandingPage {
    constructor() {
        console.log('🚀 LandingPage constructor called');
        this.autoScrollSpeed = 1; // pixels per frame
        this.observers = [];
        this.animationFrames = [];
        this.isUserInteracting = false;
        this.interactionTimeout = null;
    }

    // Static factory for Blazor
    static create() {
        console.log('🏭 LandingPage.create() called');
        return new LandingPage();
    }

    initialize() {
        console.log('✅ LandingPage.initialize() called');
        console.log('📍 Current URL:', window.location.href);

        // Wait for DOM to be ready
        if (document.readyState === 'loading') {
            console.log('⏳ DOM still loading, waiting...');
            document.addEventListener('DOMContentLoaded', () => {
                console.log('✓ DOM ready, setting up scrolls');
                this.setupScrolls();
            });
        } else {
            console.log('✓ DOM already ready, setting up scrolls immediately');
            this.setupScrolls();
        }
    }

    setupScrolls() {
        console.log('🔧 setupScrolls() called');

        // Find scroll containers
        const featuredWrapper = document.querySelector('.featured-section .scroll-wrapper');
        const testimonialsWrapper = document.querySelector('.testimonials-section .testimonials-scroll-wrapper');

        console.log('🔍 Featured wrapper found:', !!featuredWrapper);
        console.log('🔍 Testimonials wrapper found:', !!testimonialsWrapper);

        if (featuredWrapper) {
            console.log('📦 Setting up featured products scroll');
            this.setupInfiniteScroll(
                featuredWrapper,
                '.scroll-content',
                'featured',
                this.autoScrollSpeed
            );
        } else {
            console.warn('⚠️ Featured wrapper not found, retrying in 500ms');
            setTimeout(() => this.setupScrolls(), 500);
            return;
        }

        if (testimonialsWrapper) {
            console.log('💬 Setting up testimonials scroll');
            this.setupInfiniteScroll(
                testimonialsWrapper,
                '.testimonials-scroll-content',
                'testimonials',
                this.autoScrollSpeed
            );
        }
    }

    setupInfiniteScroll(wrapper, contentSelector, name, scrollSpeed) {
        const content = wrapper.querySelector(contentSelector);

        if (!content) {
            console.error(`❌ ${name}: Content selector "${contentSelector}" not found`);
            return;
        }

        console.log(`✓ ${name}: Found content element`);
        console.log(`  └─ Scroll width: ${content.scrollWidth}px`);
        console.log(`  └─ Wrapper width: ${wrapper.clientWidth}px`);

        // State for this scroll instance
        const state = {
            currentPosition: 0,
            isPaused: false,
            animationId: null,
            maxScroll: content.scrollWidth / 2, // Half because content is duplicated
            lastTimestamp: null
        };

        console.log(`  └─ Max scroll: ${state.maxScroll}px (halfway point for loop)`);

        // === AUTO-SCROLL ANIMATION (using requestAnimationFrame) ===
        const autoScroll = (timestamp) => {
            // Calculate delta time for smooth animation
            if (!state.lastTimestamp) state.lastTimestamp = timestamp;
            const deltaTime = timestamp - state.lastTimestamp;
            state.lastTimestamp = timestamp;

            // Only scroll if not paused and user not interacting
            if (!state.isPaused && !this.isUserInteracting) {
                // Move position (use deltaTime for frame-rate independence)
                state.currentPosition += scrollSpeed;

                // Loop back to start when reaching halfway point
                if (state.currentPosition >= state.maxScroll) {
                    state.currentPosition = 0;
                    console.log(`🔄 ${name}: Looped back to start`);
                }

                // PERFORMANCE: Use transform instead of scrollLeft (GPU-accelerated)
                wrapper.scrollLeft = state.currentPosition;
            }

            // Continue animation loop
            state.animationId = requestAnimationFrame(autoScroll);
        };

        // Start the animation
        state.animationId = requestAnimationFrame(autoScroll);
        this.animationFrames.push(state);
        console.log(`▶️ ${name}: Auto-scroll started`);

        // === INTERSECTION OBSERVER (Detect when items enter/leave view) ===
        const observerOptions = {
            root: wrapper,
            rootMargin: '0px',
            threshold: 0.5 // 50% visible
        };

        const observerCallback = (entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    console.log(`👁️ ${name}: Item entered view -`, entry.target.className);
                    // Could add animation classes here
                    entry.target.classList.add('in-view');
                } else {
                    entry.target.classList.remove('in-view');
                }
            });
        };

        const observer = new IntersectionObserver(observerCallback, observerOptions);
        this.observers.push(observer);

        // Observe all cards
        const cards = content.querySelectorAll('.product-card-wrapper, .testimonials-scroll-content > *');
        cards.forEach(card => observer.observe(card));
        console.log(`👁️ ${name}: Observing ${cards.length} cards`);

        // === USER INTERACTION HANDLERS ===

        // Pause on hover (desktop)
        wrapper.addEventListener('mouseenter', () => {
            console.log(`⏸️ ${name}: Paused (mouse enter)`);
            state.isPaused = true;
        });

        wrapper.addEventListener('mouseleave', () => {
            console.log(`▶️ ${name}: Resumed (mouse leave)`);
            state.isPaused = false;
        });

        // Pause on touch (mobile)
        wrapper.addEventListener('touchstart', () => {
            console.log(`⏸️ ${name}: Paused (touch start)`);
            this.handleUserInteraction();
            state.isPaused = true;
        }, { passive: true });

        wrapper.addEventListener('touchend', () => {
            console.log(`⏸️ ${name}: Touch ended, will resume in 2s`);
            setTimeout(() => {
                state.isPaused = false;
                this.isUserInteracting = false;
                console.log(`▶️ ${name}: Resumed after touch`);
            }, 2000);
        }, { passive: true });

        // Handle mouse drag
        let isDragging = false;
        let startX = 0;
        let scrollStart = 0;

        wrapper.addEventListener('mousedown', (e) => {
            isDragging = true;
            startX = e.pageX - wrapper.offsetLeft;
            scrollStart = state.currentPosition;
            wrapper.style.cursor = 'grabbing';
            state.isPaused = true;
            this.handleUserInteraction();
            console.log(`🖱️ ${name}: Drag started`);
        });

        wrapper.addEventListener('mouseleave', () => {
            if (isDragging) {
                isDragging = false;
                wrapper.style.cursor = 'grab';
                setTimeout(() => {
                    state.isPaused = false;
                    this.isUserInteracting = false;
                }, 2000);
            }
        });

        wrapper.addEventListener('mouseup', () => {
            if (isDragging) {
                isDragging = false;
                wrapper.style.cursor = 'grab';
                setTimeout(() => {
                    state.isPaused = false;
                    this.isUserInteracting = false;
                    console.log(`▶️ ${name}: Resumed after drag`);
                }, 2000);
            }
        });

        wrapper.addEventListener('mousemove', (e) => {
            if (!isDragging) return;
            e.preventDefault();

            const x = e.pageX - wrapper.offsetLeft;
            const walk = (x - startX) * 2; // Multiply for faster drag
            state.currentPosition = scrollStart - walk;

            // Keep within bounds
            if (state.currentPosition < 0) state.currentPosition = 0;
            if (state.currentPosition > state.maxScroll) state.currentPosition = state.maxScroll;

            wrapper.scrollLeft = state.currentPosition;
        });

        // Handle manual scroll (wheel/touchpad)
        let scrollTimeout;
        wrapper.addEventListener('scroll', () => {
            if (!isDragging && !this.isUserInteracting) {
                this.handleUserInteraction();
                state.isPaused = true;

                clearTimeout(scrollTimeout);
                scrollTimeout = setTimeout(() => {
                    this.isUserInteracting = false;
                    state.isPaused = false;
                    console.log(`▶️ ${name}: Resumed after manual scroll`);
                }, 2000);
            }
        }, { passive: true });

        console.log(`✅ ${name}: Infinite scroll setup complete`);
    }

    handleUserInteraction() {
        this.isUserInteracting = true;

        if (this.interactionTimeout) {
            clearTimeout(this.interactionTimeout);
        }

        this.interactionTimeout = setTimeout(() => {
            this.isUserInteracting = false;
        }, 2000);
    }

    dispose() {
        console.log('🗑️ LandingPage.dispose() called');

        // Cancel all animation frames
        this.animationFrames.forEach(state => {
            if (state.animationId) {
                cancelAnimationFrame(state.animationId);
                console.log('  └─ Cancelled animation frame');
            }
        });

        // Disconnect all observers
        this.observers.forEach(observer => {
            observer.disconnect();
            console.log('  └─ Disconnected observer');
        });

        // Clear timeouts
        if (this.interactionTimeout) {
            clearTimeout(this.interactionTimeout);
        }

        this.animationFrames = [];
        this.observers = [];

        console.log('✓ LandingPage cleanup complete');
    }
}

// Make available globally for Blazor
if (typeof window !== 'undefined') {
    window.LandingPage = LandingPage;
    console.log('🌐 LandingPage class registered globally');
}