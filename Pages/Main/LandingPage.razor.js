// Pages/Main/LandingPage.razor.js
export class LandingPage {
    constructor() {
        // ── Scroll speeds ──────────────────────────────────────────────────
        // Featured products: moderate pace
        this._featuredSpeed = 1.0;
        // Testimonials: noticeably faster than before
        this._testimonialsSpeed = 2.2;

        this._observers      = [];
        this._scrollStates   = [];
        this._setupAttempts  = 0;
        this._maxAttempts    = 15;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    static create() {
        return new LandingPage();
    }

    initialize() {
        // Defer slightly so Blazor has finished painting the DOM
        const delay = document.readyState === 'loading' ? 0 : 250;
        setTimeout(() => this._trySetup(), delay);
    }

    dispose() {
        this._scrollStates.forEach(s => {
            if (s.rafId)             cancelAnimationFrame(s.rafId);
            if (s.resumeTimeout)     clearTimeout(s.resumeTimeout);
        });
        this._observers.forEach(o => o.disconnect());
        this._scrollStates = [];
        this._observers    = [];
    }

    // ── Setup orchestration ────────────────────────────────────────────────

    _trySetup() {
        this._setupAttempts++;

        const featured     = document.querySelector('.featured-section .scroll-wrapper');
        const testimonials = document.querySelector('.testimonials-section .testimonials-scroll-wrapper');

        if (!featured && this._setupAttempts < this._maxAttempts) {
            setTimeout(() => this._trySetup(), 300);
            return;
        }

        if (featured)     this._initScroll(featured,     '.scroll-content',              this._featuredSpeed);
        if (testimonials) this._initScroll(testimonials, '.testimonials-scroll-content', this._testimonialsSpeed);

        this._initCountUp();
    }

    // ── Infinite scroll ────────────────────────────────────────────────────

    _initScroll(wrapper, contentSel, speed) {
        const content = wrapper.querySelector(contentSel);
        if (!content) return;

        // Disable smooth scrolling so programmatic assignment is instant
        wrapper.style.scrollBehavior = 'auto';

        // Content may still be laying out — wait until it's wide enough
        if (content.scrollWidth <= wrapper.clientWidth + 4) {
            if (this._setupAttempts < this._maxAttempts) {
                setTimeout(() => this._initScroll(wrapper, contentSel, speed), 300);
            }
            return;
        }

        const halfWidth = content.scrollWidth / 2;

        const state = {
            wrapper,
            halfWidth,
            speed,
            paused:        false,   // mouse hover
            userScrolling: false,   // touch / wheel / drag
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

                // Seamless loop: when we reach the halfway point (duplicate),
                // jump back to the exact same visual position at the start
                if (wrapper.scrollLeft >= state.halfWidth) {
                    wrapper.scrollLeft -= state.halfWidth;
                }
            }
            state.rafId = requestAnimationFrame(tick);
        };
        state.rafId = requestAnimationFrame(tick);

        this._scrollStates.push(state);

        // ── Pause on hover (desktop) ──
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

        // ── Mouse-wheel (track-pad horizontal swipe) ──
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
    }

    // ── Count-up animation ─────────────────────────────────────────────────

    _initCountUp() {
        const statsSection = document.getElementById('hero-stats');
        if (!statsSection) return;

        const numbers = Array.from(statsSection.querySelectorAll('.stat-number[data-target]'));
        if (!numbers.length) return;

        // Reset display to zero immediately so there's no flash of the
        // server-rendered value before animation
        numbers.forEach(el => {
            el.textContent = '0' + (el.dataset.suffix || '');
        });

        // Animate only when the stats section enters the viewport
        const io = new IntersectionObserver(entries => {
            if (!entries[0].isIntersecting) return;
            numbers.forEach(el => this._countUp(el));
            io.disconnect();
        }, { threshold: 0.4 });

        io.observe(statsSection);
        this._observers.push(io);
    }

    _countUp(el) {
        const target   = parseInt(el.dataset.target,  10) || 0;
        const suffix   = el.dataset.suffix || '';
        const duration = 1600; // ms — fast but satisfying
        const start    = performance.now();

        // Cubic ease-out: starts fast, decelerates into the final value
        const ease = t => 1 - Math.pow(1 - t, 3);

        const frame = now => {
            const elapsed  = now - start;
            const progress = Math.min(elapsed / duration, 1);
            const current  = Math.floor(ease(progress) * target);

            el.textContent = current + suffix;

            if (progress < 1) {
                requestAnimationFrame(frame);
            } else {
                // Snap to exact target (avoids floating-point rounding)
                el.textContent = target + suffix;
            }
        };

        requestAnimationFrame(frame);
    }
}

// Expose on window for Blazor interop fallback (not strictly needed with ES module pattern)
if (typeof window !== 'undefined') {
    window.LandingPage = LandingPage;
}
