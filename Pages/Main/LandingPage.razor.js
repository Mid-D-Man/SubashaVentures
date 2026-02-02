export class LandingPage {
    constructor() {
        this.scrollWrappers = [];
        this.isDragging = false;
        this.startX = 0;
        this.scrollLeft = 0;
    }

    initialize() {
        console.log('✅ LandingPage.js initialized');
        
        // Wait for DOM to be ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.setupInfiniteScroll());
        } else {
            this.setupInfiniteScroll();
        }
    }

    setupInfiniteScroll() {
        // Find all scroll wrappers
        const featuredWrapper = document.querySelector('.featured-section .scroll-wrapper');
        const testimonialsWrapper = document.querySelector('.testimonials-section .testimonials-scroll-wrapper');

        if (featuredWrapper) {
            this.setupScrollLoop(featuredWrapper, '.scroll-content', '.product-card-wrapper');
        }

        if (testimonialsWrapper) {
            this.setupScrollLoop(testimonialsWrapper, '.testimonials-scroll-content', 'div > *');
        }
    }

    setupScrollLoop(wrapper, contentSelector, itemSelector) {
        const content = wrapper.querySelector(contentSelector);
        if (!content) return;

        let isScrolling = false;
        let scrollTimeout;

        // Handle scroll event with infinite loop
        wrapper.addEventListener('scroll', () => {
            if (!isScrolling) {
                isScrolling = true;
            }

            // Clear previous timeout
            clearTimeout(scrollTimeout);

            // Set timeout to detect when scrolling stops
            scrollTimeout = setTimeout(() => {
                isScrolling = false;
                this.checkScrollPosition(wrapper, content, itemSelector);
            }, 150);
        });

        // Handle mouse drag
        wrapper.addEventListener('mousedown', (e) => {
            this.isDragging = true;
            this.startX = e.pageX - wrapper.offsetLeft;
            this.scrollLeft = wrapper.scrollLeft;
            wrapper.style.cursor = 'grabbing';
        });

        wrapper.addEventListener('mouseleave', () => {
            this.isDragging = false;
            wrapper.style.cursor = 'grab';
        });

        wrapper.addEventListener('mouseup', () => {
            this.isDragging = false;
            wrapper.style.cursor = 'grab';
        });

        wrapper.addEventListener('mousemove', (e) => {
            if (!this.isDragging) return;
            e.preventDefault();
            const x = e.pageX - wrapper.offsetLeft;
            const walk = (x - this.startX) * 2;
            wrapper.scrollLeft = this.scrollLeft - walk;
        });

        // Touch support
        let touchStartX = 0;
        let touchScrollLeft = 0;

        wrapper.addEventListener('touchstart', (e) => {
            touchStartX = e.touches[0].pageX - wrapper.offsetLeft;
            touchScrollLeft = wrapper.scrollLeft;
        });

        wrapper.addEventListener('touchmove', (e) => {
            const x = e.touches[0].pageX - wrapper.offsetLeft;
            const walk = (x - touchStartX) * 2;
            wrapper.scrollLeft = touchScrollLeft - walk;
        });

        // Store reference
        this.scrollWrappers.push({ wrapper, content, itemSelector });
    }

    checkScrollPosition(wrapper, content, itemSelector) {
        const items = content.querySelectorAll(itemSelector);
        if (items.length === 0) return;

        const itemWidth = items[0].offsetWidth;
        const gap = parseInt(getComputedStyle(content).gap) || 0;
        const totalItemWidth = itemWidth + gap;
        const halfContentWidth = (items.length / 2) * totalItemWidth;

        const scrollLeft = wrapper.scrollLeft;
        const maxScroll = content.scrollWidth - wrapper.clientWidth;

        // If scrolled near the end (past 75% of the halfway point)
        if (scrollLeft > halfContentWidth * 0.75) {
            // Reset to the beginning smoothly
            wrapper.scrollTo({
                left: 0,
                behavior: 'auto'
            });
        }
        // If scrolled to the very beginning (less than one item width)
        else if (scrollLeft < totalItemWidth) {
            // Jump to the middle section
            wrapper.scrollTo({
                left: halfContentWidth * 0.25,
                behavior: 'auto'
            });
        }
    }

    destroy() {
        // Cleanup if needed
        this.scrollWrappers = [];
    }
}

// Auto-initialize
if (typeof window !== 'undefined') {
    window.LandingPage = LandingPage;
    
    // Create instance and initialize
    const landingPageInstance = new LandingPage();
    landingPageInstance.initialize();
    
    // Re-initialize on Blazor page updates
    if (window.Blazor) {
        window.Blazor.addEventListener('enhancedload', () => {
            landingPageInstance.initialize();
        });
    }
}
