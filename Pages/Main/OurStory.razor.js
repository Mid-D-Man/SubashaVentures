// Pages/Main/OurStory.razor.js
export class OurStory {
    constructor() {
        this._observers = [];
        this._pageEl    = null;
        this._parallaxHandler = null;
    }

    static create() { return new OurStory(); }

    initialize() {
        // Small delay so Blazor finishes painting
        setTimeout(() => {
            this._initReveal();
            this._initParallax();
        }, 80);
    }

    // ── Scroll reveal ──────────────────────────────────────────────────────

    _initReveal() {
        const targets = document.querySelectorAll(
            '.reveal-up, .reveal-left, .reveal-right'
        );
        if (!targets.length) return;

        const io = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                const el    = entry.target;
                const delay = parseInt(el.style.getPropertyValue('--delay') || '0', 10);

                setTimeout(() => {
                    el.classList.add('revealed');
                }, delay);

                io.unobserve(el);
            });
        }, {
            threshold:  0.14,
            rootMargin: '0px 0px -40px 0px'
        });

        targets.forEach(el => io.observe(el));
        this._observers.push(io);
    }

    // ── Subtle hero parallax ───────────────────────────────────────────────
    // Moves the hero shapes slightly as the user scrolls — very light touch.

    _initParallax() {
        const hero = document.querySelector('.os-hero');
        if (!hero) return;

        // The scroll container is .page-content, not window
        this._pageEl = document.querySelector('.page-content');
        if (!this._pageEl) return;

        const shape1 = hero.querySelector('.os-shape-1');
        const shape2 = hero.querySelector('.os-shape-2');

        if (!shape1 && !shape2) return;

        this._parallaxHandler = () => {
            const scrolled = this._pageEl.scrollTop;
            if (shape1) shape1.style.transform = `translateY(${scrolled * 0.12}px)`;
            if (shape2) shape2.style.transform = `translate(-20px, ${30 + scrolled * -0.08}px)`;
        };

        this._pageEl.addEventListener('scroll', this._parallaxHandler, { passive: true });
    }

    dispose() {
        this._observers.forEach(o => o.disconnect());
        this._observers = [];

        if (this._pageEl && this._parallaxHandler) {
            this._pageEl.removeEventListener('scroll', this._parallaxHandler);
        }
        this._pageEl         = null;
        this._parallaxHandler = null;
    }
}

if (typeof window !== 'undefined') {
    window.OurStory = OurStory;
}
