// Intercept clics, auxclick, submit and JS window.open function to prevent from opening a new tab/window. Call http://localhost:xxx?_open=<encoded_original_url> instead

const baseURL = new URL(location.href);
if (baseURL.hostname == 'localhost') {
	const isExternalUrl = (urlStr) => {
		try {
			const url = new URL(urlStr, location.href);
			// Skip if protocol is not http/https/ftp/ftps (mailto:, javascript:, data:, etc.)
			if (!['http:', 'https:', 'ftp', 'ftps'].includes(url.protocol)) {
				return false;
			}
			// Skip if host is local
			return url.hostname != 'localhost';
		} catch (e) {
			return false;
		}
	};

	const callServerIfExternalUrl = (url, e) => {
		if (isExternalUrl(url)) {
			e?.preventDefault();
			e?.stopImmediatePropagation();
			fetch(baseURL.origin + '?_open=' + encodeURIComponent(url));
			return true;
		}
	};

	// Find anchor element from event composedPath
	const findAnchorFromEvent = (e) => {
		for (const node of e.composedPath()) {
			if (node?.tagName?.toLowerCase() === 'a' && node?.href) {
				return node;
			}
		}
		// Fallback: check target
		let t = e.target;
		while (t && t !== document) {
			if (t?.tagName?.toLowerCase() === 'a' && t.href) {
				return t;
			}
			t = t.parentNode;
		}
		return null;
	};

	// Left click handle
	const handleClickEvent = (e) => {
		const href = findAnchorFromEvent(e)?.href;
		callServerIfExternalUrl(href, e);
	};

	// Middle click handle
	const handleAuxClickEvent = (e) => {
		if (e.button === 1 || e.button === 4) {
			const href = findAnchorFromEvent(e)?.href;
			callServerIfExternalUrl(href, e);
		}
	};

	const handleSubmitEvent = (e) => {
		const form = e.target?.tagName?.toLowerCase() === 'form' ? e.target : null;
		const action = form?.getAttribute('action') || window.location.href;
		callServerIfExternalUrl(action, e);
	};

	// Attach listeners in capture phase so we can stop navigation early
	document.addEventListener('click', handleClickEvent, { capture: true, passive: false });
	document.addEventListener('auxclick', handleAuxClickEvent, { capture: true, passive: false });
	document.addEventListener('submit', handleSubmitEvent, { capture: true, passive: false });
	
	const originalOpen = window.open;
	window.open = function (url, target, features) {
		try {
			if (callServerIfExternalUrl(url)) {
console.log('call server');
				return null;
			}
		} catch {}
console.log('fail to call server');
		return originalOpen.apply(this, arguments);
	};
}