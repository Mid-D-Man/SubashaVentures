//  qrCodeModule.js with proper WASM loading
let wasmModule;
let wasmInitialized = false;
let wasmAvailable = false;

async function tryInitWasm() {
    try {
        const wasmModuleUrl = new URL('../wasm/qr_code_generator.js', import.meta.url);
        console.log('Attempting to load WASM from:', wasmModuleUrl.href);

        const wasmModule = await import(wasmModuleUrl.href);

        const wasmBinaryUrl = new URL('../wasm/qr_code_generator_bg.wasm', import.meta.url);
        console.log('Loading WASM binary from:', wasmBinaryUrl.href);

        await wasmModule.default(wasmBinaryUrl.href);

        wasmInitialized = true;
        wasmAvailable = true;
        console.log("WASM QR module initialized successfully");
        return wasmModule;
    } catch (error) {
        console.warn("WASM QR module not available, using fallback:", error.message);
        wasmAvailable = false;
        return null;
    }
}

async function tryInitWasmAlternative() {
    try {
        const wasmUrl = new URL('../wasm/qr_code_generator_bg.wasm', import.meta.url);
        const response = await fetch(wasmUrl.href);

        if (!response.ok) {
            throw new Error(`WASM file not found: ${response.status} ${response.statusText}`);
        }

        const contentType = response.headers.get('content-type');
        console.log('WASM file content-type:', contentType);

        if (contentType && !contentType.includes('application/wasm') && !contentType.includes('application/octet-stream')) {
            console.warn('WASM file served with incorrect MIME type:', contentType);
        }

        const jsModule = await import('../wasm/qr_code_generator.js');
        const wasmBytes = await response.arrayBuffer();
        await jsModule.default(wasmBytes);

        wasmInitialized = true;
        wasmAvailable = true;
        console.log("WASM QR module initialized via alternative method");
        return jsModule;
    } catch (error) {
        console.warn("Alternative WASM loading failed:", error.message);
        return null;
    }
}

export async function initWasm() {
    if (!wasmInitialized && !wasmAvailable) {
        wasmModule = await tryInitWasm();
        if (!wasmModule) {
            wasmModule = await tryInitWasmAlternative();
        }
    }
    return wasmInitialized;
}

// Enhanced fallback QR code generation
function generateFallbackQRCode(text, size, darkColor, lightColor) {
    const cellSize = Math.floor(size / 25);
    const margin = cellSize * 2;
    const finderSize = cellSize * 7;

    let svg = `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">`;
    svg += `<rect width="100%" height="100%" fill="${lightColor}"/>`;

    const positions = [
        [margin, margin],
        [size - margin - finderSize, margin],
        [margin, size - margin - finderSize]
    ];

    positions.forEach(([x, y]) => {
        svg += `<rect x="${x}" y="${y}" width="${finderSize}" height="${finderSize}" fill="${darkColor}"/>`;
        svg += `<rect x="${x + cellSize}" y="${y + cellSize}" width="${finderSize - 2 * cellSize}" height="${finderSize - 2 * cellSize}" fill="${lightColor}"/>`;
        svg += `<rect x="${x + 2 * cellSize}" y="${y + 2 * cellSize}" width="${finderSize - 4 * cellSize}" height="${finderSize - 4 * cellSize}" fill="${darkColor}"/>`;
    });

    for (let i = 9; i < 16; i++) {
        for (let j = 9; j < 16; j++) {
            if ((i + j) % 2 === 0) {
                svg += `<rect x="${margin + i * cellSize}" y="${margin + j * cellSize}" width="${cellSize}" height="${cellSize}" fill="${darkColor}"/>`;
            }
        }
    }

    // Add a text label so the user knows it's a placeholder
    svg += `<text x="${size / 2}" y="${size - 8}" text-anchor="middle" font-size="9" fill="${darkColor}" font-family="monospace">Scan manually</text>`;
    svg += '</svg>';
    return svg;
}

// FIX: Try standard QR first; if "data too long", retry with "L" (Low) error correction
// which roughly quadruples data capacity and handles TOTP URIs comfortably.
export async function generateQrCode(text, size, darkColor, lightColor) {
    try {
        await initWasm();

        if (wasmAvailable && wasmModule) {
            try {
                return wasmModule.generate_qr_code(text, size, darkColor, lightColor);
            } catch (primaryError) {
                const msg = (primaryError.message || '').toLowerCase();
                if (msg.includes('data too long') || msg.includes('too long') || msg.includes('capacity')) {
                    console.warn('QR data too long at default error correction; retrying with "L" level for higher capacity');
                    try {
                        // generate_enhanced_qr_code accepts error_level as 5th param
                        return wasmModule.generate_enhanced_qr_code(
                            text, size, darkColor, lightColor,
                            'L',   // error_level – lowest correction = maximum data capacity
                            null,  // logo_url
                            false, // use_gradient
                            null, null, null, // gradient params
                            0     // margin
                        );
                    } catch (retryError) {
                        console.error('Retry with L error correction also failed:', retryError.message);
                        throw retryError;
                    }
                }
                throw primaryError;
            }
        } else {
            console.log("WASM unavailable – using fallback QR code");
            return generateFallbackQRCode(text, size, darkColor, lightColor);
        }
    } catch (error) {
        console.error("Error generating QR code, using fallback:", error);
        return generateFallbackQRCode(text, size, darkColor, lightColor);
    }
}

export async function generateEnhancedQrCode(text, size, darkColor, lightColor, options = {}) {
    try {
        await initWasm();

        // Use low error correction by default for enhanced too, to handle long TOTP URIs
        const errorLevel = options.errorLevel || 'L';

        let baseSvg;
        if (wasmAvailable && wasmModule) {
            try {
                baseSvg = wasmModule.generate_enhanced_qr_code(
                    text, size, darkColor, lightColor,
                    errorLevel,
                    options.logoUrl || null,
                    options.useGradient || false,
                    options.gradientDirection || null,
                    options.gradientColor1 || null,
                    options.gradientColor2 || null,
                    options.qrMargin || 0
                );
            } catch (e) {
                console.warn('Enhanced WASM QR failed, falling back to JS generation:', e.message);
                baseSvg = generateFallbackQRCode(text, size, darkColor, lightColor);
            }
        } else {
            baseSvg = generateFallbackQRCode(text, size, darkColor, lightColor);
        }

        return applyEnhancements(baseSvg, size, darkColor, lightColor, options);
    } catch (error) {
        console.error("Error generating enhanced QR code:", error);
        throw error;
    }
}

function applyEnhancements(baseSvg, size, darkColor, lightColor, options) {
    try {
        const parser = new DOMParser();
        const svgDoc = parser.parseFromString(baseSvg, "image/svg+xml");
        const svgElement = svgDoc.documentElement;

        const parserError = svgDoc.querySelector('parsererror');
        if (parserError) {
            console.error('SVG parsing error:', parserError.textContent);
            return baseSvg;
        }

        if (options.useGradient) {
            const gradId = `qrGradient_${Math.random().toString(36).substring(2, 9)}`;
            const gradientDirection = options.gradientDirection || "linear-x";
            const gradColor1 = options.gradientColor1 || darkColor;
            const gradColor2 = options.gradientColor2 || darkColor;

            const defs = document.createElementNS("http://www.w3.org/2000/svg", "defs");
            let gradient;

            if (gradientDirection === "radial") {
                gradient = document.createElementNS("http://www.w3.org/2000/svg", "radialGradient");
                gradient.setAttribute("cx", "50%");
                gradient.setAttribute("cy", "50%");
                gradient.setAttribute("r", "50%");
            } else {
                gradient = document.createElementNS("http://www.w3.org/2000/svg", "linearGradient");
                switch (gradientDirection) {
                    case "linear-x":
                        gradient.setAttribute("x1", "0%"); gradient.setAttribute("y1", "50%");
                        gradient.setAttribute("x2", "100%"); gradient.setAttribute("y2", "50%");
                        break;
                    case "linear-y":
                        gradient.setAttribute("x1", "50%"); gradient.setAttribute("y1", "0%");
                        gradient.setAttribute("x2", "50%"); gradient.setAttribute("y2", "100%");
                        break;
                    case "diagonal":
                        gradient.setAttribute("x1", "0%"); gradient.setAttribute("y1", "0%");
                        gradient.setAttribute("x2", "100%"); gradient.setAttribute("y2", "100%");
                        break;
                }
            }

            gradient.setAttribute("id", gradId);

            const stop1 = document.createElementNS("http://www.w3.org/2000/svg", "stop");
            stop1.setAttribute("offset", "0%");
            stop1.setAttribute("stop-color", gradColor1);

            const stop2 = document.createElementNS("http://www.w3.org/2000/svg", "stop");
            stop2.setAttribute("offset", "100%");
            stop2.setAttribute("stop-color", gradColor2);

            gradient.appendChild(stop1);
            gradient.appendChild(stop2);
            defs.appendChild(gradient);
            svgElement.insertBefore(defs, svgElement.firstChild);

            const allElements = svgElement.querySelectorAll('path, rect, circle, polygon');
            allElements.forEach(element => {
                const fillColor = element.getAttribute('fill');
                if (fillColor && fillColor.toLowerCase() === darkColor.toLowerCase()) {
                    element.setAttribute('fill', `url(#${gradId})`);
                }
            });
        }

        if (options.logoUrl) {
            const logoSizeRatio = options.logoSizeRatio || 0.25;
            const logoSize = Math.floor(size * logoSizeRatio);
            const logoX = Math.floor((size - logoSize) / 2);
            const logoY = Math.floor((size - logoSize) / 2);

            const logoBg = document.createElementNS("http://www.w3.org/2000/svg", "rect");
            logoBg.setAttribute("x", logoX - 5);
            logoBg.setAttribute("y", logoY - 5);
            logoBg.setAttribute("width", logoSize + 10);
            logoBg.setAttribute("height", logoSize + 10);
            logoBg.setAttribute("fill", "white");
            logoBg.setAttribute("rx", "5");
            svgElement.appendChild(logoBg);

            const image = document.createElementNS("http://www.w3.org/2000/svg", "image");
            image.setAttribute("href", options.logoUrl);
            image.setAttribute("x", logoX);
            image.setAttribute("y", logoY);
            image.setAttribute("width", logoSize);
            image.setAttribute("height", logoSize);
            image.setAttribute("preserveAspectRatio", "xMidYMid slice");
            svgElement.appendChild(image);

            if (options.addLogoBorder) {
                const rect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
                rect.setAttribute("x", logoX);
                rect.setAttribute("y", logoY);
                rect.setAttribute("width", logoSize);
                rect.setAttribute("height", logoSize);
                rect.setAttribute("fill", "none");
                rect.setAttribute("stroke", options.logoBorderColor || "white");
                rect.setAttribute("stroke-width", options.logoBorderWidth || "2");
                if (options.logoBorderRadius) {
                    rect.setAttribute("rx", options.logoBorderRadius);
                    rect.setAttribute("ry", options.logoBorderRadius);
                }
                svgElement.appendChild(rect);
            }
        }

        const serializer = new XMLSerializer();
        return serializer.serializeToString(svgDoc);
    } catch (error) {
        console.error("Error applying enhancements:", error);
        return baseSvg;
    }
}

// FIX: setSvgContent now retries if the element isn't in DOM yet (race condition guard)
export function setSvgContent(elementId, svgContent) {
    const element = document.getElementById(elementId);
    if (element) {
        element.innerHTML = svgContent;
    } else {
        console.warn(`Element '${elementId}' not in DOM yet – retrying in 100ms`);
        setTimeout(() => {
            const retryEl = document.getElementById(elementId);
            if (retryEl) {
                retryEl.innerHTML = svgContent;
            } else {
                console.error(`Element with ID '${elementId}' not found after retry`);
            }
        }, 100);
    }
}

export function getModuleStatus() {
    return {
        wasmInitialized,
        wasmAvailable,
        hasWasmModule: !!wasmModule
    };
}
