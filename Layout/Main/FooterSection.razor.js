window.footerScrollHandler = {
    dotNetRef: null,
    scrollThreshold: 300,
    
    initialize: function(dotNetReference) {
        this.dotNetRef = dotNetReference;
        this.setupScrollListener();
    },
    
    setupScrollListener: function() {
        let ticking = false;
        
        const checkScroll = () => {
            const pageContent = document.querySelector('.page-content');
            if (!pageContent) return;
            
            const scrollTop = pageContent.scrollTop;
            const shouldShow = scrollTop > this.scrollThreshold;
            
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('UpdateScrollTopVisibility', shouldShow);
            }
            
            ticking = false;
        };
        
        const onScroll = () => {
            if (!ticking) {
                window.requestAnimationFrame(checkScroll);
                ticking = true;
            }
        };
        
        const pageContent = document.querySelector('.page-content');
        if (pageContent) {
            pageContent.addEventListener('scroll', onScroll);
        }
    },
    
    scrollToTop: function() {
        const pageContent = document.querySelector('.page-content');
        if (pageContent) {
            pageContent.scrollTo({
                top: 0,
                behavior: 'smooth'
            });
        }
    }
};
