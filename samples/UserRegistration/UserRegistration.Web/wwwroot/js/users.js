// Modal flow: a button loads a partial view into the modal; the form inside posts with fetch().
// 200 = done (reload the list), 422 = the server re-rendered the form with errors (swap it in).
(() => {
    const modalEl = document.getElementById('app-modal');
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const body = document.getElementById('app-modal-body');
    const title = document.getElementById('app-modal-title');

    const show = html => {
        body.innerHTML = html;
        $.validator.unobtrusive.parse(body); // client-side rules for the freshly loaded form
        body.querySelector('input:not([type=hidden])')?.focus();
    };

    document.querySelectorAll('[data-modal-url]').forEach(btn =>
        btn.addEventListener('click', async () => {
            title.textContent = btn.dataset.modalTitle;
            show(await (await fetch(btn.dataset.modalUrl)).text());
            modal.show();
        }));

    body.addEventListener('submit', async e => {
        e.preventDefault();
        const form = e.target;
        if (!$(form).valid()) return;

        const res = await fetch(form.action, { method: 'POST', body: new FormData(form) });
        if (res.status === 422) {
            show(await res.text());
        } else if (res.ok) {
            const { message } = await res.json();
            sessionStorage.setItem('flash', message);
            location.reload();
        } else {
            body.insertAdjacentHTML('afterbegin', '<div class="alert alert-danger m-3">Something went wrong. Please try again.</div>');
        }
    });

    const flash = sessionStorage.getItem('flash');
    if (flash) {
        sessionStorage.removeItem('flash');
        const area = document.getElementById('toast-area');
        area.textContent = flash;
        area.classList.remove('d-none');
    }
})();
