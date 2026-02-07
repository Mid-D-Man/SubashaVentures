// Pages/Main/LandingPage.razor.js
export class LandingPage {
    constructor() {
        this.scrollSpeed = 0.5; // pixels per frame
        this.observers = [];
        this.scrollInstances = [];
    }

    static create() {
        return new LandingPage();
    }

    initialize() {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => {
                this.setupScrolls();
            });
        } else {
            setTimeout(() => this.setupScrolls(), 100);
        }
    }

    setupScrolls() {
        const featuredWrapper = document.querySelector('.featured-section .scroll-wrapper');
        const testimonialsWrapper = document.querySelector('.testimonials-section .testimonials-scroll-wrapper');

        if (featuredWrapper) {
            this.setupInfiniteScroll(featuredWrapper, '.scroll-content', this.scrollSpeed);
        } else {
            setTimeout(() => this.setupScrolls(), 500);
            return;
        }

        if (testimonialsWrapper) {
            this.setupInfiniteScroll(testimonialsWrapper, '.testimonials-scroll-content', this.scrollSpeed);
        }
    }

    setupInfiniteScroll(wrapper, contentSelector, scrollSpeed) {
        const content = wrapper.querySelector(contentSelector);
        if (!content) return;

        const state = {
            isPaused: false,
            isUserScrolling: false,
            animationId: null,
            lastScrollLeft: 0,
            userScrollTimeout: null
        };

        // Auto-scroll function
        const autoScroll = () => {
            if (!state.isPaused && !state.isUserScrolling) {
                const maxScroll = content.scrollWidth / 2;
                
                wrapper.scrollLeft += scrollSpeed;

                // Loop back when reaching halfway point
                if (wrapper.scrollLeft >= maxScroll) {
                    wrapper.scrollLeft = 0;
                }
            }

            state.animationId = requestAnimationFrame(autoScroll);
        };

        // Start auto-scroll
        state.animationId = requestAnimationFrame(autoScroll);
        this.scrollInstances.push(state);

        // Intersection Observer for fade effects
        const observer = new IntersectionObserver(
            (entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        entry.target.classList.add('in-view');
                    } else {
                        entry.target.classList.remove('in-view');
                    }
                });
            },
            { root: wrapper, threshold: 0.5 }
        );

        this.observers.push(observer);

        const cards = content.querySelectorAll('.product-card-wrapper, .testimonials-scroll-content > *');
        cards.forEach(card => observer.observe(card));

        // Pause on hover (desktop)
        wrapper.addEventListener('mouseenter', () => {
            state.isPaused = true;
        });

        wrapper.addEventListener('mouseleave', () => {
            state.isPaused = false;
        });

        // Handle touch interactions (mobile)
        wrapper.addEventListener('touchstart', () => {
            state.isUserScrolling = true;
            if (state.userScrollTimeout) {
                clearTimeout(state.userScrollTimeout);
            }
        }, { passive: true });

        wrapper.addEventListener('touchend', () => {
            state.userScrollTimeout = setTimeout(() => {
                state.isUserScrolling = false;
            }, 3000);
        }, { passive: true });

        // Handle manual scroll (mouse wheel, trackpad)
        let scrollTimeout;
        wrapper.addEventListener('scroll', () => {
            const currentScrollLeft = wrapper.scrollLeft;
            
            // Detect if user is manually scrolling
            if (Math.abs(currentScrollLeft - state.lastScrollLeft) > scrollSpeed * 2) {
                state.isUserScrolling = true;
                
                clearTimeout(scrollTimeout);
                scrollTimeout = setTimeout(() => {
                    state.isUserScrolling = false;
                }, 3000);
            }
            
            state.lastScrollLeft = currentScrollLeft;
        }, { passive: true });

        // Mouse drag support
        let isDragging = false;
        let startX = 0;
        let scrollStart = 0;

        wrapper.addEventListener('mousedown', (e) => {
            isDragging = true;
            startX = e.pageX - wrapper.offsetLeft;
            scrollStart = wrapper.scrollLeft;
            wrapper.style.cursor = 'grabbing';
            state.isPaused = true;
        });

        const stopDragging = () => {
            if (isDragging) {
                isDragging = false;
                wrapper.style.cursor = 'grab';
                setTimeout(() => {
                    state.isPaused = false;
                }, 2000);
            }
        };

        wrapper.addEventListener('mouseleave', stopDragging);
        wrapper.addEventListener('mouseup', stopDragging);

        wrapper.addEventListener('mousemove', (e) => {
            if (!isDragging) return;
            e.preventDefault();

            const x = e.pageX - wrapper.offsetLeft;
            const walk = (x - startX) * 2;
            wrapper.scrollLeft = scrollStart - walk;
        });
    }

    dispose() {
        // Cancel all animations
        this.scrollInstances.forEach(state => {
            if (state.animationId) {
                cancelAnimationFrame(state.animationId);
            }
            if (state.userScrollTimeout) {
                clearTimeout(state.userScrollTimeout);
            }
        });

        // Disconnect observers
        this.observers.forEach(observer => observer.disconnect());

        this.scrollInstances = [];
        this.observers = [];
    }
}

if (typeof window !== 'undefined') {
    window.LandingPage = LandingPage;
}
