// Pages/Main/LandingPage.razor.js
export class LandingPage {
    constructor() {
        // ── Scroll speeds ──────────────────────────────────────────────
        this._featuredSpeed     = 1.0;
        this._testimonialsSpeed = 2.2;

        this._observers    = [];
        this._scrollStates = [];
        // Keep a direct reference to the featured scroll state so we can
        // cancel + recreate it when Blazor replaces the DOM after async load.
        this._featuredState = null;

        this._setupAttempts = 0;
        this._maxAttempts   = 15;
    }

    // ── Public API ─────────────────────────────────────────────────────

    static create() { return new LandingPage(); }

    initialize() {
        const delay = document.readyState === 'loading' ? 0 : 250;
        setTimeout(() => this._trySetup(), delay);
    }

    /**
     * Called by Blazor (LandingPage.razor.cs) after featured products
     * finish loading and StateHasChanged() has triggered a re-render.
     * Blazor replaces the entire .scroll-wrapper element, so we must
     * re-query the DOM and restart the RAF loop on the fresh node.
     */
    reinitFeaturedScroll() {
        // 1. Cancel existing featured loop (targets a stale/detached node)
        if (this._featuredState) {
            if (this._featuredState.rafId)        cancelAnimationFrame(this._featuredState.rafId);
            if (this._featuredState.resumeTimeout) clearTimeout(this._featuredState.resumeTimeout);
            this._scrollStates = this._scrollStates.filter(s => s !== this._featuredState);
            this._featuredState = null;
        }

        // 2. Re-query fresh DOM element
        const wrapper = document.querySelector('.featured-section .scroll-wrapper');
        if (!wrapper) return;

        const state = this._setupScrollState(wrapper, '.scroll-content', this._featuredSpeed);
        if (state) {
            this._featuredState = state;
            this._scrollStates.push(state);
        }
    }

    dispose() {
        this._scrollStates.forEach(s => {
            if (s.rafId)             cancelAnimationFrame(s.rafId);
            if (s.resumeTimeout)     clearTimeout(s.resumeTimeout);
        });
        this._observers.forEach(o => o.disconnect());
        this._scrollStates  = [];
        this._featuredState = null;
        this._observers     = [];
    }

    // ── Setup orchestration ────────────────────────────────────────────

    _trySetup() {
        this._setupAttempts++;

        const featured     = document.querySelector('.featured-section .scroll-wrapper');
        const testimonials = document.querySelector('.testimonials-section .testimonials-scroll-wrapper');

        if (!featured && !testimonials && this._setupAttempts < this._maxAttempts) {
            setTimeout(() => this._trySetup(), 300);
            return;
        }

        // ── Testimonials: always hardcoded, init immediately ──
        if (testimonials) {
            const state = this._setupScrollState(testimonials, '.testimonials-scroll-content', this._testimonialsSpeed);
            if (state) this._scrollStates.push(state);
        }

        // ── Featured products: loaded async by Blazor.
        //    Attempt an immediate init in case products are already rendered
        //    (e.g., fast network / cached).  If the content is not wide enough
        //    yet (still showing skeletons), Blazor will call reinitFeaturedScroll()
        //    after the real cards paint.
        if (featured) {
            const content = featured.querySelector('.scroll-content');
            if (content && content.scrollWidth > featured.clientWidth + 4) {
                const state = this._setupScrollState(featured, '.scroll-content', this._featuredSpeed);
                if (state) {
                    this._featuredState = state;
                    this._scrollStates.push(state);
                }
            }
            // If not wide enough, reinitFeaturedScroll() will handle it.
        }

        this._initCountUp();
    }

    // ── Core scroll initialiser ────────────────────────────────────────
    // Returns the state object on success, null if content not ready.

    _setupScrollState(wrapper, contentSel, speed) {
        const content = wrapper.querySelector(contentSel);
        if (!content) return null;

        // Disable smooth scrolling so programmatic assignment is instant
        wrapper.style.scrollBehavior = 'auto';

        // Content must be wider than the viewport to scroll
        if (content.scrollWidth <= wrapper.clientWidth + 4) return null;

        const halfWidth = content.scrollWidth / 2;

        const state = {
            wrapper,
            halfWidth,
            speed,
            paused:        false,
            userScrolling: false,
            rafId:         null,
            resumeTimeout: null,
            dragging:      false,
            dragStartX:    0,
            dragStartLeft: 0,
        };

        // ── rAF loop ──
        const tick = () => {
            if (!state.paused && !state.userScrolling) {
                wrapper.scrollLeft += state.speed;
                if (wrapper.scrollLeft >= state.halfWidth) {
                    wrapper.scrollLeft -= state.halfWidth;
                }
            }
            state.rafId = requestAnimationFrame(tick);
        };
        state.rafId = requestAnimationFrame(tick);

        // ── Hover pause (desktop) ──
        wrapper.addEventListener('mouseenter', () => { state.paused = true; });
        wrapper.addEventListener('mouseleave', () => {
            if (!state.dragging) state.paused = false;
        });

        // ── Touch ──
        wrapper.addEventListener('touchstart', () => {
            state.userScrolling = true;
            clearTimeout(state.resumeTimeout);
        }, { passive: true });

        wrapper.addEventListener('touchend', () => {
            state.resumeTimeout = setTimeout(() => { state.userScrolling = false; }, 2500);
        }, { passive: true });

        // ── Trackpad / wheel horizontal swipe ──
        wrapper.addEventListener('wheel', () => {
            state.userScrolling = true;
            clearTimeout(state.resumeTimeout);
            state.resumeTimeout = setTimeout(() => { state.userScrolling = false; }, 2500);
        }, { passive: true });

        // ── Click-drag ──
        wrapper.addEventListener('mousedown', e => {
            state.dragging      = true;
            state.paused        = true;
            state.userScrolling = true;
            state.dragStartX    = e.pageX - wrapper.offsetLeft;
            state.dragStartLeft = wrapper.scrollLeft;
            wrapper.style.cursor = 'grabbing';
            clearTimeout(state.resumeTimeout);
        });

        const stopDrag = () => {
            if (!state.dragging) return;
            state.dragging       = false;
            wrapper.style.cursor = 'grab';
            state.resumeTimeout  = setTimeout(() => {
                state.paused        = false;
                state.userScrolling = false;
            }, 2000);
        };

        wrapper.addEventListener('mouseup',    stopDrag);
        wrapper.addEventListener('mouseleave', stopDrag);

        wrapper.addEventListener('mousemove', e => {
            if (!state.dragging) return;
            e.preventDefault();
            const x    = e.pageX - wrapper.offsetLeft;
            const walk = (x - state.dragStartX) * 1.5;
            wrapper.scrollLeft = state.dragStartLeft - walk;
        });

        return state;
    }

    // ── Count-up animation ─────────────────────────────────────────────

    _initCountUp() {
        const statsSection = document.getElementById('hero-stats');
        if (!statsSection) return;

        const numbers = Array.from(statsSection.querySelectorAll('.stat-number[data-target]'));
        if (!numbers.length) return;

        numbers.forEach(el => {
            el.textContent = '0' + (el.dataset.suffix || '');
        });

        const io = new IntersectionObserver(entries => {
            if (!entries[0].isIntersecting) return;
            numbers.forEach(el => this._countUp(el));
            io.disconnect();
        }, { threshold: 0.4 });

        io.observe(statsSection);
        this._observers.push(io);
    }

    _countUp(el) {
        const target   = parseInt(el.dataset.target, 10) || 0;
        const suffix   = el.dataset.suffix || '';
        const duration = 1600;
        const start    = performance.now();
        const ease     = t => 1 - Math.pow(1 - t, 3);

        const frame = now => {
            const elapsed  = now - start;
            const progress = Math.min(elapsed / duration, 1);
            const current  = Math.floor(ease(progress) * target);
            el.textContent = current + suffix;
            if (progress < 1) {
                requestAnimationFrame(frame);
            } else {
                el.textContent = target + suffix;
            }
        };
        requestAnimationFrame(frame);
    }
}

if (typeof window !== 'undefined') {
    window.LandingPage = LandingPage;
}
