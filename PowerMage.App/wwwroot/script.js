const isOpenClass = "modal-is-open";
const openingClass = "modal-is-opening";
const closingClass = "modal-is-closing";
const scrollbarWidthCssVar = "--pico-scrollbar-width";
const animationDuration = 400; // ms
let visibleModal = null;

const toggleModal = (event) => {
    event.preventDefault();
    const modal = document.getElementById(event.currentTarget.dataset.target);
    if (!modal) return;
    modal && (modal.open ? closeModal(modal) : openModal(modal));
};

const openModal = (modal) => {
    const { documentElement: html } = document;
    const scrollbarWidth = getScrollbarWidth();
    if (scrollbarWidth) {
        html.style.setProperty(scrollbarWidthCssVar, `${scrollbarWidth}px`);
    }
    html.classList.add(isOpenClass, openingClass);
    setTimeout(() => {
        visibleModal = modal;
        html.classList.remove(openingClass);
    }, animationDuration);
    modal.showModal();
};

const closeModal = (modal) => {
    visibleModal = null;
    const { documentElement: html } = document;
    html.classList.add(closingClass);
    setTimeout(() => {
        html.classList.remove(closingClass, isOpenClass);
        html.style.removeProperty(scrollbarWidthCssVar);
        modal.close();
    }, animationDuration);
};

document.addEventListener("click", (event) => {
    if (visibleModal === null) return;
    const modalContent = visibleModal.querySelector("article");
    const isClickInside = modalContent.contains(event.target);
    !isClickInside && closeModal(visibleModal);
});

document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && visibleModal) {
        closeModal(visibleModal);
    }
});

const getScrollbarWidth = () => {
    const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
    return scrollbarWidth;
};

const isScrollbarVisible = () => {
    return document.body.scrollHeight > screen.height;
};


(function () {
    const OFFSET = 14;
    let tip = null;
    let titleEl = null;
    let valueEl = null;
    let currentColorClass = null;

    function getTip() {
        if (!tip) {
            tip = document.createElement('div');
            tip.className = 'chart-tooltip';

            titleEl = document.createElement('div');
            titleEl.className = 'chart-tooltip-title';

            valueEl = document.createElement('div');
            valueEl.className = 'chart-tooltip-value';

            tip.appendChild(titleEl);
            tip.appendChild(valueEl);
            document.body.appendChild(tip);
        }
        return tip;
    }

    function position(e) {
        const t = getTip();
        const rect = t.getBoundingClientRect();
        let x = e.clientX + OFFSET;
        let y = e.clientY + OFFSET;

        if (x + rect.width > window.innerWidth - 8) x = e.clientX - rect.width - OFFSET;
        if (y + rect.height > window.innerHeight - 8) y = e.clientY - rect.height - OFFSET;

        t.style.transform = `translate(${Math.max(8, x)}px, ${Math.max(8, y)}px)`;
    }

    function fill(target) {
        let title = target.getAttribute('data-tooltip-title');
        const color = target.getAttribute('data-tooltip-color');

        if (title) {
            title = new Date(title * 1000).toLocaleDateString('en-US', {
                weekday: 'long',
                day: 'numeric',
                month: 'long',
                year: 'numeric'
            });
        }
        titleEl.textContent = title;
        titleEl.style.display = title ? '' : 'none';

        valueEl.textContent = target.getAttribute('data-tooltip');

        if (currentColorClass) valueEl.classList.remove(currentColorClass);
        currentColorClass = color ? `${color}-color` : null;
        if (currentColorClass) valueEl.classList.add(currentColorClass);
    }

    document.addEventListener('mouseover', function (e) {
        const target = e.target.closest('[data-tooltip]');
        if (!target) return;
        const t = getTip();
        fill(target);
        t.classList.add('visible');
        position(e);
    });

    document.addEventListener('mousemove', function (e) {
        if (tip && tip.classList.contains('visible')) position(e);
    });

    document.addEventListener('mouseout', function (e) {
        const target = e.target.closest('[data-tooltip]');
        if (!target) return;
        if (target.contains(e.relatedTarget)) return;
        tip?.classList.remove('visible');
    });

    document.addEventListener('scroll', () => tip?.classList.remove('visible'), true);
})();