// Pages/Main/LandingPage.razor.js
export class LandingPage {
    constructor() {
        this._featuredSpeed     = 1.0;
        this._testimonialsSpeed = 2.2;

        this._observers         = [];
        this._scrollStates      = [];
        this._featuredState     = null;
        this._testimonialsState = null;

        this._setupAttempts      = 0;
        this._maxAttempts        = 20;
        this._reinitAttempts     = 0;
        this._maxReinitAttempts  = 8;
        this._countUpDone        = false;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    static create() { return new LandingPage(); }

    initialize() {
        const delay = document.readyState === 'loading' ? 0 : 250;
        setTimeout(() => this._trySetup(), delay);
    }

    /**
     * Called by Blazor after featured products finish loading and the DOM
     * has been re-rendered. Cancels the stale skeleton-card loop and starts
     * a fresh one on the real product cards.
     */
    reinitFeaturedScroll() {
        if (this._featuredState) {
            if (this._featuredState.rafId)        cancelAnimationFrame(this._featuredState.rafId);
            if (this._featuredState.resumeTimeout) clearTimeout(this._featuredState.resumeTimeout);
            this._scrollStates  = this._scrollStates.filter(s => s !== this._featuredState);
            this._featuredState = null;
        }
        this._reinitAttempts = 0;
        // Small rAF-aligned delay so the browser finishes painting new cards
        // before we measure scrollWidth
        requestAnimationFrame(() => setTimeout(() => this._tryReinitFeatured(), 50));
    }

    dispose() {
        this._scrollStates.forEach(s => {
            if (s.rafId)         cancelAnimationFrame(s.rafId);
            if (s.resumeTimeout) clearTimeout(s.resumeTimeout);
        });
        this._observers.forEach(o => o.disconnect());
        this._scrollStates      = [];
        this._featuredState     = null;
        this._testimonialsState = null;
        this._observers         = [];
    }

    // ── Setup orchestration ────────────────────────────────────────────────

    _trySetup() {
        this._setupAttempts++;

        const featured     = document.querySelector('.featured-section .scroll-wrapper');
        const testimonials = document.querySelector('.testimonials-section .testimonials-scroll-wrapper');

        // Nothing in DOM yet — keep waiting
        if (!featured && !testimonials && this._setupAttempts < this._maxAttempts) {
            setTimeout(() => this._trySetup(), 300);
            return;
        }

        let needsRetry = false;

        // ── Testimonials ─────────────────────────────────────────────────
        if (testimonials && !this._testimonialsState) {
            const state = this._setupScrollState(
                testimonials, '.testimonials-scroll-content', this._testimonialsSpeed);
            if (state) {
                this._testimonialsState = state;
                this._scrollStates.push(state);
            } else {
                needsRetry = true; // DOM painted but layout not finalised yet
            }
        }

        // ── Featured products ────────────────────────────────────────────
        // Try an immediate init in case products are already rendered
        // (fast network / cached). If showing skeletons and they are wide
        // enough (they usually are), this starts the scroll on skeletons
        // and reinitFeaturedScroll() will replace it once real cards load.
        if (featured && !this._featuredState) {
            const state = this._setupScrollState(
                featured, '.scroll-content', this._featuredSpeed);
            if (state) {
                this._featuredState = state;
                this._scrollStates.push(state);
            }
            // Not wide enough yet → reinitFeaturedScroll() called from Blazor
        }

        // Retry if testimonials weren't ready
        if (needsRetry && this._setupAttempts < this._maxAttempts) {
            setTimeout(() => this._trySetup(), 300);
            return;
        }

        if (!this._countUpDone) {
            this._countUpDone = true;
            this._initCountUp();
        }
    }

    _tryReinitFeatured() {
        this._reinitAttempts++;
        const wrapper = document.querySelector('.featured-section .scroll-wrapper');
        if (!wrapper) return;

        const state = this._setupScrollState(wrapper, '.scroll-content', this._featuredSpeed);
        if (state) {
            this._featuredState = state;
            this._scrollStates.push(state);
        } else if (this._reinitAttempts < this._maxReinitAttempts) {
            setTimeout(() => this._tryReinitFeatured(), 300);
        }
    }

    // ── Core scroll initialiser ────────────────────────────────────────────

    _setupScrollState(wrapper, contentSel, speed) {
        const content = wrapper.querySelector(contentSel);
        if (!content) return null;

        wrapper.style.scrollBehavior = 'auto';

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

        // ── Hover pause ──
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

        // ── Trackpad / wheel ──
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

    // ── Count-up animation ─────────────────────────────────────────────────

    _initCountUp() {
        const statsSection = document.getElementById('hero-stats');
        if (!statsSection) return;

        const numbers = Array.from(
            statsSection.querySelectorAll('.stat-number[data-target]'));
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
            el.textContent = Math.floor(ease(progress) * target) + suffix;
            if (progress < 1) requestAnimationFrame(frame);
            else el.textContent = target + suffix;
        };
        requestAnimationFrame(frame);
    }
}

if (typeof window !== 'undefined') {
    window.LandingPage = LandingPage;
}
