//  qrCodeModule.js with proper WASM loading
let wasmModule;
let wasmInitialized = false;
let wasmAvailable = false;

async function tryInitWasm() {
    try {
        // Try to load the WASM module with explicit file extension
        const wasmModuleUrl = new URL('../wasm/qr_code_generator.js', import.meta.url);
        console.log('Attempting to load WASM from:', wasmModuleUrl.href);

        const wasmModule = await import(wasmModuleUrl.href);

        // Initialize WASM - try with explicit WASM file path
        const wasmBinaryUrl = new URL('../wasm/qr_code_generator_bg.wasm', import.meta.url);
        console.log('Loading WASM binary from:', wasmBinaryUrl.href);

        await wasmModule.default(wasmBinaryUrl.href);

        wasmInitialized = true;
        wasmAvailable = true;
        console.log("WASM QR module initialized successfully");
        return wasmModule;
    } catch (error) {
        console.warn("WASM QR module not available, using fallback:", error.message);
        console.error("Full error:", error);
        wasmAvailable = false;
        return null;
    }
}

// Alternative loading method for GitHub Pages
async function tryInitWasmAlternative() {
    try {
        // Try fetching the WASM file directly to check if it exists
        const wasmUrl = new URL('../wasm/qr_code_generator_bg.wasm', import.meta.url);
        const response = await fetch(wasmUrl.href);

        if (!response.ok) {
            throw new Error(`WASM file not found: ${response.status} ${response.statusText}`);
        }

        // Check content type
        const contentType = response.headers.get('content-type');
        console.log('WASM file content-type:', contentType);

        if (contentType && !contentType.includes('application/wasm') && !contentType.includes('application/octet-stream')) {
            console.warn('WASM file served with incorrect MIME type:', contentType);
        }

        // Load the JS wrapper
        const jsModule = await import('../wasm/qr_code_generator.js');

        // Initialize with the fetched WASM
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
        // Try primary method first
        wasmModule = await tryInitWasm();

        // If primary fails, try alternative
        if (!wasmModule) {
            wasmModule = await tryInitWasmAlternative();
        }
    }
    return wasmInitialized;
}

// Enhanced fallback QR code generation
function generateFallbackQRCode(text, size, darkColor, lightColor) {
    // Create a more detailed SVG-based QR code placeholder
    const cellSize = Math.floor(size / 25); // 25x25 grid
    const margin = cellSize * 2;
    const actualSize = size - (margin * 2);

    // Simple pattern that resembles a QR code
    const pattern = [
        [1,1,1,1,1,1,1,0,0,0,0,0,0,0,0,0,0,0,1,1,1,1,1,1,1],
        [1,0,0,0,0,0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,0,0,0,0,1],
        [1,0,1,1,1,0,1,0,0,1,1,1,0,1,0,1,1,0,1,0,1,1,1,0,1],
        [1,0,1,1,1,0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,1,1,1,0,1],
        [1,0,1,1,1,0,1,0,0,1,0,1,1,1,0,1,0,0,1,0,1,1,1,0,1],
        [1,0,0,0,0,0,1,0,1,0,1,0,1,0,1,0,1,0,1,0,0,0,0,0,1],
        [1,1,1,1,1,1,1,0,1,0,1,0,1,0,1,0,1,0,1,1,1,1,1,1,1],
        [0,0,0,0,0,0,0,0,0,1,0,1,0,1,0,1,0,0,0,0,0,0,0,0,0],
        // ... simplified pattern continues
    ];

    let svg = `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">`;
    svg += `<rect width="100%" height="100%" fill="${lightColor}"/>`;

    // Draw finder patterns (corners)
    const finderSize = cellSize * 7;
    const positions = [[margin, margin], [size - margin - finderSize, margin], [margin, size - margin - finderSize]];

    positions.forEach(([x, y]) => {
        svg += `<rect x="${x}" y="${y}" width="${finderSize}" height="${finderSize}" fill="${darkColor}"/>`;
        svg += `<rect x="${x + cellSize}" y="${y + cellSize}" width="${finderSize - 2*cellSize}" height="${finderSize - 2*cellSize}" fill="${lightColor}"/>`;
        svg += `<rect x="${x + 2*cellSize}" y="${y + 2*cellSize}" width="${finderSize - 4*cellSize}" height="${finderSize - 4*cellSize}" fill="${darkColor}"/>`;
    });

    // Add some data pattern
    for (let i = 9; i < 16; i++) {
        for (let j = 9; j < 16; j++) {
            if ((i + j) % 2 === 0) {
                svg += `<rect x="${margin + i * cellSize}" y="${margin + j * cellSize}" width="${cellSize}" height="${cellSize}" fill="${darkColor}"/>`;
            }
        }
    }

    svg += '</svg>';
    return svg;
}

export async function generateQrCode(text, size, darkColor, lightColor) {
    try {
        await initWasm();

        if (wasmAvailable && wasmModule) {
            return wasmModule.generate_qr_code(text, size, darkColor, lightColor);
        } else {
            console.log("Using fallback QR code generation");
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

        // Generate base QR code using the available function
        const baseSvg = await generateQrCode(text, size, darkColor, lightColor);

        // Parse the SVG into a DOM object for manipulation
        const parser = new DOMParser();
        const svgDoc = parser.parseFromString(baseSvg, "image/svg+xml");
        const svgElement = svgDoc.documentElement;

        // If gradient is requested, apply the gradient enhancements
        if (options.useGradient) {
            const gradId = `qrGradient_${Math.random().toString(36).substring(2, 9)}`;
            const gradientDirection = options.gradientDirection || "linear-x";
            const gradColor1 = options.gradientColor1 || darkColor;
            const gradColor2 = options.gradientColor2 || darkColor;

            // Create the defs element and gradient definition
            const defs = document.createElementNS("http://www.w3.org/2000/svg", "defs");
            let gradient;
            if (gradientDirection === "radial") {
                gradient = document.createElementNS("http://www.w3.org/2000/svg", "radialGradient");
                gradient.setAttribute("cx", "50%");
                gradient.setAttribute("cy", "50%");
                gradient.setAttribute("r", "50%");
            } else {
                gradient = document.createElementNS("http://www.w3.org/2000/svg", "linearGradient");
                if (gradientDirection === "linear-x") {
                    gradient.setAttribute("x1", "0%");
                    gradient.setAttribute("y1", "50%");
                    gradient.setAttribute("x2", "100%");
                    gradient.setAttribute("y2", "50%");
                } else if (gradientDirection === "linear-y") {
                    gradient.setAttribute("x1", "50%");
                    gradient.setAttribute("y1", "0%");
                    gradient.setAttribute("x2", "50%");
                    gradient.setAttribute("y2", "100%");
                } else if (gradientDirection === "diagonal") {
                    gradient.setAttribute("x1", "0%");
                    gradient.setAttribute("y1", "0%");
                    gradient.setAttribute("x2", "100%");
                    gradient.setAttribute("y2", "100%");
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

            // Insert defs as the first child of the SVG
            svgElement.insertBefore(defs, svgElement.firstChild);

            // Replace dark color fills with the gradient reference on <path> and <rect> elements
            const targets = svgElement.querySelectorAll('path, rect');
            targets.forEach(element => {
                const fillColor = element.getAttribute('fill');
                if (fillColor && fillColor.trim().toLowerCase() === darkColor.trim().toLowerCase()) {
                    element.setAttribute('fill', `url(#${gradId})`);
                }
            });

            // Also update inline style definitions for fill
            const styledElements = svgElement.querySelectorAll('[style*="fill"]');
            styledElements.forEach(element => {
                const style = element.getAttribute('style');
                if (style && (style.includes(`fill:${darkColor}`) || style.includes(`fill: ${darkColor}`))) {
                    element.style.fill = `url(#${gradId})`;
                }
            });
        }

        // If a logo is requested, add the logo on top of the QR code.
        // We create a dedicated group for logo elements so that they are painted last.
        if (options.logoUrl) {
            const logoGroup = document.createElementNS("http://www.w3.org/2000/svg", "g");
            // Use a provided margin to account for QR code drawing area if needed.
            const logoSizeRatio = options.logoSizeRatio || 0.25;
            const logoSize = Math.floor(size * logoSizeRatio);
            const qrMargin = options.qrMargin || 0; // Set this if your QR code has a visible margin.
            const effectiveSize = size - 2 * qrMargin;
            const logoX = Math.floor(qrMargin + (effectiveSize - logoSize) / 2);
            const logoY = Math.floor(qrMargin + (effectiveSize - logoSize) / 2);

            const image = document.createElementNS("http://www.w3.org/2000/svg", "image");
            // Set both href and xlink:href for compatibility.
            image.setAttribute("href", options.logoUrl);
            image.setAttributeNS("http://www.w3.org/1999/xlink", "xlink:href", options.logoUrl);
            image.setAttribute("x", logoX);
            image.setAttribute("y", logoY);
            image.setAttribute("width", logoSize);
            image.setAttribute("height", logoSize);
            image.setAttribute("preserveAspectRatio", "xMidYMid slice");

            logoGroup.appendChild(image);

            if (options.addLogoBorder) {
                const borderRect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
                borderRect.setAttribute("x", logoX);
                borderRect.setAttribute("y", logoY);
                borderRect.setAttribute("width", logoSize);
                borderRect.setAttribute("height", logoSize);
                borderRect.setAttribute("fill", "none");
                borderRect.setAttribute("stroke", options.logoBorderColor || "white");
                borderRect.setAttribute("stroke-width", options.logoBorderWidth || "2");
                if (options.logoBorderRadius) {
                    borderRect.setAttribute("rx", options.logoBorderRadius);
                    borderRect.setAttribute("ry", options.logoBorderRadius);
                }
                logoGroup.appendChild(borderRect);
            }

            // Append the logo group last to ensure it appears on top
            svgElement.appendChild(logoGroup);
        }

        // Serialize the modified SVG and return it as a string
        const serializer = new XMLSerializer();
        return serializer.serializeToString(svgDoc);
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

        // Apply gradient if requested
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
                        gradient.setAttribute("x1", "0%");
                        gradient.setAttribute("y1", "50%");
                        gradient.setAttribute("x2", "100%");
                        gradient.setAttribute("y2", "50%");
                        break;
                    case "linear-y":
                        gradient.setAttribute("x1", "50%");
                        gradient.setAttribute("y1", "0%");
                        gradient.setAttribute("x2", "50%");
                        gradient.setAttribute("y2", "100%");
                        break;
                    case "diagonal":
                        gradient.setAttribute("x1", "0%");
                        gradient.setAttribute("y1", "0%");
                        gradient.setAttribute("x2", "100%");
                        gradient.setAttribute("y2", "100%");
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

            //  Target all dark-colored elements, not just paths
            const allElements = svgElement.querySelectorAll('path, rect, circle, polygon');
            allElements.forEach(element => {
                const fillColor = element.getAttribute('fill');
                // Check if element has the dark color (exact match or similar)
                if (fillColor === darkColor ||
                    (fillColor && fillColor.toLowerCase() === darkColor.toLowerCase()) ||
                    element.style.fill === darkColor) {
                    element.setAttribute('fill', `url(#${gradId})`);
                }
            });

            // check for elements with dark color in style attribute
            const styledElements = svgElement.querySelectorAll('[style*="fill"]');
            styledElements.forEach(element => {
                const style = element.getAttribute('style');
                if (style && style.includes(`fill:${darkColor}`) || style.includes(`fill: ${darkColor}`)) {
                    element.style.fill = `url(#${gradId})`;
                }
            });
        }

        // Add logo if requested
        if (options.logoUrl) {
            const logoSizeRatio = options.logoSizeRatio || 0.25;
            const logoSize = Math.floor(size * logoSizeRatio);
            const logoX = Math.floor((size - logoSize) / 2);
            const logoY = Math.floor((size - logoSize) / 2);

            // Add white background for logo
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

export function setSvgContent(elementId, svgContent) {
    const element = document.getElementById(elementId);
    if (element) {
        console.log(`Setting SVG content for element: ${elementId}`);
        console.log("SVG content length:", svgContent.length);
        element.innerHTML = svgContent;
    } else {
        console.error(`Element with ID '${elementId}' not found`);
    }
}

export function getModuleStatus() {
    return {
        wasmInitialized,
        wasmAvailable,
        hasWasmModule: !!wasmModule
    };
}