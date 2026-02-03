// Layout/Main/FooterSection.razor.js
export class FooterSection {
    constructor() {
        this.dotNetRef = null;
        this.scrollThreshold = 300;
        this.ticking = false;
        this.pageContent = null;
    }

    initialize(dotNetReference) {
        console.log('✅ FooterSection.js initialized');
        this.dotNetRef = dotNetReference;
        this.setupScrollListener();
    }

    setupScrollListener() {
        this.pageContent = document.querySelector('.page-content');

        if (!this.pageContent) {
            console.warn('⚠️ .page-content not found, retrying...');
            setTimeout(() => this.setupScrollListener(), 100);
            return;
        }

        const checkScroll = () => {
            const scrollTop = this.pageContent.scrollTop;
            const shouldShow = scrollTop > this.scrollThreshold;

            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('UpdateScrollTopVisibility', shouldShow)
                    .catch(err => console.error('Error invoking UpdateScrollTopVisibility:', err));
            }

            this.ticking = false;
        };

        const onScroll = () => {
            if (!this.ticking) {
                window.requestAnimationFrame(checkScroll);
                this.ticking = true;
            }
        };

        this.pageContent.addEventListener('scroll', onScroll);
        console.log('✓ Scroll listener attached to .page-content');
    }

    scrollToTop() {
        if (this.pageContent) {
            this.pageContent.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        } else {
            console.warn('⚠️ Cannot scroll - .page-content not found');
        }
    }

    dispose() {
        console.log('🗑️ FooterSection disposed');
        this.dotNetRef = null;
        this.pageContent = null;
    }
}

// Auto-initialize for Blazor
if (typeof window !== 'undefined') {
    window.FooterSection = FooterSection;

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            console.log('📄 DOM ready - FooterSection available');
        });
    }
}