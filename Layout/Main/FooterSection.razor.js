// Layout/Main/FooterSection.razor.js
export class FooterSection {
    constructor() {
        this.dotNetRef = null;
        this.scrollThreshold = 300;
        this.ticking = false;
        this.pageContent = null;
        this.scrollHandler = null;
    }

    initialize(dotNetReference) {
        this.dotNetRef = dotNetReference;
        this.setupScrollListener();
    }

    setupScrollListener() {
        this.pageContent = document.querySelector('.page-content');

        if (!this.pageContent) {
            setTimeout(() => this.setupScrollListener(), 100);
            return;
        }

        const checkScroll = () => {
            const scrollTop = this.pageContent.scrollTop;
            const shouldShow = scrollTop > this.scrollThreshold;

            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('UpdateScrollTopVisibility', shouldShow)
                    .catch(() => {});
            }

            this.ticking = false;
        };

        this.scrollHandler = () => {
            if (!this.ticking) {
                window.requestAnimationFrame(checkScroll);
                this.ticking = true;
            }
        };

        this.pageContent.addEventListener('scroll', this.scrollHandler);
    }

    scrollToTop() {
        if (this.pageContent) {
            this.pageContent.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        }
    }

    dispose() {
        if (this.pageContent && this.scrollHandler) {
            this.pageContent.removeEventListener('scroll', this.scrollHandler);
        }
        this.dotNetRef = null;
        this.pageContent = null;
        this.scrollHandler = null;
    }
}

if (typeof window !== 'undefined') {
    window.FooterSection = FooterSection;
}
