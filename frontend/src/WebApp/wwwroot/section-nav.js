window.cohereScrollToSection = (elementId) => {
    const element = document.getElementById(elementId);
    if (!element) {
        return;
    }

    element.scrollIntoView({ behavior: 'smooth', block: 'start' });

    const basePath = window.location.pathname + window.location.search;
    const nextUrl = `${basePath}#${elementId}`;
    if (window.location.href !== `${window.location.origin}${nextUrl}`) {
        history.replaceState(null, '', nextUrl);
    }
};

window.cohereExpandAndScrollToSection = (elementId) => {
    requestAnimationFrame(() => {
        requestAnimationFrame(() => {
            cohereScrollToSection(elementId);
        });
    });
};
