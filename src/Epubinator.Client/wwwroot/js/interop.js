/**
 * Epubinator JS Interop Layer
 * Handles: IndexedDB via Dexie, CSS theming, scroll tracking, PWA SW update/install prompts.
 */
window.epubInterop = (function () {
    'use strict';

    let db = null;

    // Capture the beforeinstallprompt event as early as possible so it can be
    // triggered later when the user clicks the install button.
    let _installPromptEvent = null;
    let _installDotNetRef   = null;
    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        _installPromptEvent = e;
        if (_installDotNetRef) {
            _installDotNetRef.invokeMethodAsync('OnInstallAvailable');
        }
    });
    // If the app is installed, hide any install UI
    window.addEventListener('appinstalled', function () {
        _installPromptEvent = null;
        if (_installDotNetRef) {
            _installDotNetRef.invokeMethodAsync('OnAppInstalled');
        }
    });

    function initDb() {
        if (db) return;
        db = new Dexie('EpubinatorDB');
        db.version(1).stores({
            books:    'id',   // metadata
            bookData: 'id'    // raw epub bytes
        });
    }

    return {

        // ── IndexedDB ─────────────────────────────────────────────────────────

        addBook: async function (id, metaJson, bytes) {
            initDb();
            const meta = typeof metaJson === 'string' ? JSON.parse(metaJson) : metaJson;
            meta.id = id;
            await db.transaction('rw', db.books, db.bookData, async () => {
                await db.books.put(meta);
                await db.bookData.put({ id: id, data: bytes });
            });
        },

        getBookBytes: async function (id) {
            initDb();
            const record = await db.bookData.get(id);
            return record ? record.data : null;
        },

        getAllBookMetadata: async function () {
            initDb();
            const books = await db.books.toArray();
            return books;
        },

        deleteBook: async function (id) {
            initDb();
            await db.transaction('rw', db.books, db.bookData, async () => {
                await db.books.delete(id);
                await db.bookData.delete(id);
            });
        },

        // ── Theme ─────────────────────────────────────────────────────────────

        applyTheme: function (theme, fontFamily, fontSize) {
            document.documentElement.setAttribute('data-theme', theme);
            document.documentElement.style.setProperty('--reader-font', fontFamily);
            document.documentElement.style.setProperty('--reader-font-size', fontSize + 'px');
        },

        // ── Scroll ────────────────────────────────────────────────────────────

        getScrollPercent: function () {
            const el = document.getElementById('reader-scroll-container');
            if (!el) return 0;
            const max = el.scrollHeight - el.clientHeight;
            if (max <= 0) return 0;
            return el.scrollTop / max;
        },

        scrollToPercent: function (percent) {
            const el = document.getElementById('reader-scroll-container');
            if (!el) return;
            // Use rAF to ensure layout is complete before scrolling
            requestAnimationFrame(function () {
                const max = el.scrollHeight - el.clientHeight;
                el.scrollTop = percent * max;
            });
        },

        // ── PWA Service Worker update ─────────────────────────────────────────

        registerSwUpdateListener: function (dotNetRef) {
            if (!('serviceWorker' in navigator)) return;
            navigator.serviceWorker.ready.then(function (reg) {
                // If a service worker is already waiting (e.g. the user left the
                // tab open while an update installed), notify immediately.
                if (reg.waiting && navigator.serviceWorker.controller) {
                    dotNetRef.invokeMethodAsync('OnUpdateAvailable');
                }

                reg.addEventListener('updatefound', function () {
                    const newWorker = reg.installing;
                    if (!newWorker) return;
                    newWorker.addEventListener('statechange', function () {
                        // 'installed' + existing controller = update waiting
                        if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                            dotNetRef.invokeMethodAsync('OnUpdateAvailable');
                        }
                    });
                });
            });
        },

        // ── PWA Install prompt ────────────────────────────────────────────────

        registerInstallPromptListener: function (dotNetRef) {
            _installDotNetRef = dotNetRef;
            // If beforeinstallprompt already fired before Blazor was ready, notify now.
            if (_installPromptEvent) {
                dotNetRef.invokeMethodAsync('OnInstallAvailable');
            }
        },

        showInstallPrompt: async function () {
            if (!_installPromptEvent) return false;
            await _installPromptEvent.prompt();
            const choice = await _installPromptEvent.userChoice;
            _installPromptEvent = null;
            return choice.outcome === 'accepted';
        },

        skipWaitingAndReload: function () {
            if (!('serviceWorker' in navigator)) {
                window.location.reload();
                return;
            }
            navigator.serviceWorker.addEventListener('controllerchange', function () {
                window.location.reload();
            });
            navigator.serviceWorker.ready.then(function (reg) {
                if (reg.waiting) {
                    reg.waiting.postMessage({ type: 'SKIP_WAITING' });
                } else {
                    window.location.reload();
                }
            });
        },

        // ── Bootstrap Offcanvas ───────────────────────────────────────────────

        closeOffcanvas: function (id) {
            const el = document.getElementById(id);
            if (!el) return;
            const bsInstance = bootstrap.Offcanvas.getInstance(el);
            if (bsInstance) bsInstance.hide();
        }
    };
})();
