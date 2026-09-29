// Behaviour MudBlazor lacks, for the shared UI: grid auto-fit (no interop, a
// document listener) and the one named helper the logs view calls.

// The files a pick is holding until each of them has been sent, by the ticket
// pickFiles answered with. Not on the input element they came from: that
// element belongs to a view that closes, and a file waiting its turn in the
// queue must not go with it.
const wslcAgentPicked = new Map();
let wslcAgentPicks = 0;

// What one pick answers, whichever picker answered it. The files are held by a
// ticket of their own, and by position inside it, so that sending does not
// depend on the element or the dialog still being there (the view that owns it
// closes, and a queued file's turn can come long after) and two sends never
// take each other's file. A pick with nothing in it parks nothing: its ticket
// would wait in the page for a send that is never coming, and the page is not
// reloaded for days.
function wslcAgentPark(chosen) {
    if (chosen.length === 0) {
        return { ticket: '', files: [] };
    }

    const ticket = String(++wslcAgentPicks);
    wslcAgentPicked.set(ticket, chosen);
    return { ticket, files: chosen.map((file, index) => ({ index, name: file.name, size: file.size })) };
}

// The UI's own interop, by name: wslcAgent.scrollToEnd (logs' jump to end),
// wslcAgent.pickFiles and wslcAgent.sendFile (the web UI's upload: only the
// browser can send a file of gigabytes without holding it in memory),
// wslcAgent.isHandheld (which side the narrow navigation opens from),
// wslcAgent.clientPlatform (which native client Home offers) and
// wslcAgent.zoom (the page zoom of the title bar's control), wslcAgent.views,
// wslcAgent.logs, wslcAgent.theme, wslcAgent.dashboard and Home v2.5's
// wslcAgent.dashboardV25 (what this device remembers of the lists, the Logs
// page, the theme and the dashboards).
window.wslcAgent = {
    // The app scaled as a browser's zoom scales it, in the browser and both native
    // clients alike, remembered on this device: --wslc-zoom, which the stylesheet
    // applies to the app, its dialogs, toasts and menu contents (never to the layer
    // MudBlazor positions menus in, which would carry them off screen). Without a
    // percent it only answers the one in use.
    zoom(percent) {
        if (percent) {
            try {
                localStorage.setItem('wslcAgent.zoom', String(percent));
            } catch {
                // Private windows: the zoom still applies, it is just not remembered.
            }
        }
        const current = percent || Number(wslcAgentStoredZoom()) || 100;
        document.documentElement.style.setProperty('--wslc-zoom', String(current / 100));
        return current;
    },

    // The dashboard's own zoom, out of design (the owner, 27 September 2026):
    // --bz-dash-zoom, which the dashboard's stylesheet applies to its canvas
    // alone, on top of the page's; remembered on this device apart from it,
    // one for each page and view ("system/desktop", "user/mobile"…), kept as
    // one JSON object. Without a percent it applies and answers the one kept.
    dashboardZoom(key, percent) {
        let kept = {};
        try {
            const read = JSON.parse(localStorage.getItem('wslcAgent.dashboardZoom') || '{}');
            kept = read && typeof read === 'object' ? read : {};
        } catch {
            kept = {};
        }
        if (percent) {
            kept[key] = percent;
            try {
                localStorage.setItem('wslcAgent.dashboardZoom', JSON.stringify(kept));
            } catch {
                // Private windows: the zoom still applies, it is just not remembered.
            }
        }
        const current = percent || Number(kept[key]) || 100;
        document.documentElement.style.setProperty('--bz-dash-zoom', String(current / 100));
        return current;
    },

    // How the dashboard's view is fitted to a screen of another aspect, out of
    // design — "fit", "fill", "width", "stretch" or "fluid", the first as a television offers them (the
    // owner, 28 September 2026) — kept on this device beside the dashboard's
    // zoom, one for each page and view, as one JSON object. Without a mode it
    // only answers the one kept, "fit" where none is.
    dashboardViewFit(key, mode) {
        let kept = {};
        try {
            const read = JSON.parse(localStorage.getItem('wslcAgent.dashboardViewFit') || '{}');
            kept = read && typeof read === 'object' ? read : {};
        } catch {
            kept = {};
        }
        if (mode) {
            kept[key] = mode;
            try {
                localStorage.setItem('wslcAgent.dashboardViewFit', JSON.stringify(kept));
            } catch {
                // Private windows: the mode still applies, it is just not remembered.
            }
        }
        return mode || kept[key] || 'fit';
    },

    // Table or cards per list, as one string ("Container=cards;ImageSummary=table"),
    // kept on this device: the choice belongs to the client, not to the agent, so
    // closing the app and opening it again finds every list as it was left. Without
    // a value it only answers the one stored.
    views(stored) {
        try {
            if (stored !== undefined && stored !== null) {
                localStorage.setItem('wslcAgent.views', stored);
            }
            return localStorage.getItem('wslcAgent.views');
        } catch {
            // Private windows: the choice still works, it is just not remembered.
            return null;
        }
    },

    // The Logs page as it was left, as one string ("hide=images;level=INFO;
    // follow=1;grid=812"), kept on this device like the table-or-cards choice:
    // coming back to the page finds its toggles, its level, Autorefresh and the
    // divider where it was. Without a value it only answers the one stored.
    logs(stored) {
        try {
            if (stored !== undefined && stored !== null) {
                localStorage.setItem('wslcAgent.logs', stored);
            }
            return localStorage.getItem('wslcAgent.logs');
        } catch {
            // Private windows: the page still works, it is just not remembered.
            return null;
        }
    },

    // The dashboard's cards, in the order, the set and the sizes the user left
    // them, as one JSON string (DashboardPreference writes and reads it), kept
    // on this device like the table-or-cards choice: the arrangement belongs to
    // the client, not to the agent. Without a value it only answers the one
    // stored.
    dashboard(stored) {
        return window.wslcAgent.remember('wslcAgent.dashboard', stored);
    },

    // Where this device reads the dashboard from: "device", its own storage, or
    // "user", the agent, so the phone and the desktop open the same one. The
    // choice is the device's (the owner, 22 September 2026), so a phone can
    // keep its own while the desktop follows the user. Without a value it only
    // answers the one stored.
    dashboardScope(stored) {
        return window.wslcAgent.remember('wslcAgent.dashboardScope', stored);
    },

    // Home v2.5's dashboard kept on this device, both its views as one JSON
    // string (DeviceDashboardStore), in a place of its own beside today's
    // Home's: neither reads the other (the owner, 26 September 2026). A key
    // of its own too beside v2's, which v2.5 never reads: it starts from
    // nothing (docs/home/v2.5/spec.md, decision 7).
    dashboardV25(stored) {
        return window.wslcAgent.remember('wslcAgent.dashboardV25', stored);
    },

    // Home v2.5's designs not saved yet, every place's, page's and view's in
    // one JSON string (DeviceDashboardDrafts): nothing reaches where the
    // dashboard is kept without Save, and this keeps the design through a
    // power cut or a lost connection until then (the owner, 28 September 2026).
    dashboardV25Draft(stored) {
        return window.wslcAgent.remember('wslcAgent.dashboardV25Draft', stored);
    },

    // Where this device keeps Home v2's dashboard: "device" or "user", the
    // agent, remembered apart from today's Home's choice.
    dashboardV2Scope(stored) {
        return window.wslcAgent.remember('wslcAgent.dashboardV2Scope', stored);
    },

    // Which of Home v2's dashboard's pages this device looked at last, System
    // or User, opened again the next time (the owner, 26 September 2026).
    dashboardV2Page(stored) {
        return window.wslcAgent.remember('wslcAgent.dashboardV2Page', stored);
    },

    // What this device remembers under a key: written when a value is given,
    // and the one stored answered. Private windows remember nothing, and the
    // application works as a device that never chose.
    remember(key, stored) {
        try {
            if (stored !== undefined && stored !== null) {
                localStorage.setItem(key, stored);
            }
            return localStorage.getItem(key);
        } catch {
            return null;
        }
    },

    // How long a finger holds a card, a part or a corner still before it is in
    // the hand (the owner, 22 September 2026): until then a swipe scrolls the
    // dashboard — the emulator's mouse wheel arrives as one — and the wheel
    // never moves or resizes anything. A mouse takes them at once.
    dashboardHoldMs: 400,

    // How still the window stands before the dashboard is placed and drawn
    // again (the owner, 22 September 2026: while the window is being dragged
    // or the phone turned, nothing is shown, and what it settles at is shown
    // once). Long enough that a window dragged across several column counts
    // is measured at the end alone, short enough that letting go feels like
    // the dashboard coming straight back.
    dashboardSettleMs: 250,

    // A grid's cell, read from the grid itself — its first resolved column and
    // row tracks and its gaps — never assumed. The row comes from the resolved
    // rows, not from grid-auto-rows: inside a card that is 0 (a part below the
    // card's last row takes no height), and the card's rows are the tracks.
    // The dashboard's columns are the ones that fit its width, not the tracks
    // it has: a card still standing past the edge of a narrowed window keeps
    // extra tracks alive, and counting those the page would never learn the
    // window had narrowed (the owner, 22 September 2026: it rearranged the
    // first time only).
    dashboardCell(grid) {
        const style = getComputedStyle(grid);
        const gapX = parseFloat(style.columnGap) || 0;
        return {
            width: parseFloat(style.gridTemplateColumns) || 0,
            height: parseFloat(style.gridTemplateRows) || parseFloat(style.gridAutoRows) || 0,
            gapX,
            gapY: parseFloat(style.rowGap) || 0,
            columns: grid.classList.contains('wslc-dash-grid')
                ? window.wslcAgent.dashboardFit(grid, style, gapX)
                : style.gridTemplateColumns.split(' ').length,
            // The columns the grid has, which a hand can reach by scrolling:
            // more than fit when the cards reach past the window's edge.
            tracks: style.gridTemplateColumns.split(' ').length,
        };
    },

    // Screen pixels per layout pixel of an element: the page zoom (--wslc-zoom),
    // which scales the dashboard as it scales the rest of the application. The
    // pointer and a bounding rectangle answer in screen pixels, a grid's cells
    // in layout pixels; everything the hand does is divided by this before it
    // is measured in cells, or at any zoom but 100 % a card would start its
    // resize, and land its drag, somewhere it is not (the owner, 22 September
    // 2026).
    dashboardScale(el) {
        return el.offsetWidth > 0 ? el.getBoundingClientRect().width / el.offsetWidth : 1;
    },

    // How many of the dashboard's narrowest columns fit its width, as
    // auto-fill counts them: the width a column may not go under is read off a
    // probe sized by --wslc-dash-column, which the grid is laid by, so no number is
    // written twice. An absolutely placed probe takes no cell.
    dashboardFit(grid, style, gapX) {
        const probe = document.createElement('div');
        probe.style.cssText = 'position: absolute; visibility: hidden; height: 0; width: var(--wslc-dash-column);';
        grid.appendChild(probe);
        const column = probe.offsetWidth;
        probe.remove();
        const width = grid.clientWidth - (parseFloat(style.paddingLeft) || 0) - (parseFloat(style.paddingRight) || 0);
        return column > 0 ? Math.max(1, Math.floor((width + gapX) / (column + gapX))) : 1;
    },

    // Whether an area of a grid is free: it overlaps none of the grid's own
    // items but `item` itself. The one rule for a move and a resize, of a card
    // or a part (the owner, 22 September 2026): an item goes only where there
    // is room, and nothing else moves to make it. Items carry their cells in
    // data-x/y/w/h, so this and DashboardLayout.IsFree judge the same cells.
    dashboardFree(grid, item, area) {
        const overlaps = (a, b) => a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
        return ![...grid.querySelectorAll(':scope > .wslc-dash-item')].some((other) =>
            other !== item && Number(other.dataset.x) >= 0 && overlaps(area, {
                x: Number(other.dataset.x), y: Number(other.dataset.y), w: Number(other.dataset.w), h: Number(other.dataset.h),
            }));
    },

    // The pane an element scrolls in: its nearest ancestor that scrolls.
    scrollParent(el) {
        for (let parent = el.parentElement; parent; parent = parent.parentElement) {
            if (/auto|scroll/.test(getComputedStyle(parent).overflowY)) {
                return parent;
            }
        }
        return null;
    },

    // While a drag on the dashboard lasts, its pane scrolls by itself when the
    // pointer is near its top or bottom edge — faster the nearer — and keeps
    // scrolling while the pointer rests there, so a card can be carried or
    // grown past what the window shows (the owner, 22 September 2026: dragged
    // to a hole above, the card came loose from the hand). `pointerY` says
    // where the pointer is now; `scrolled` is told after every step, for the
    // drag to follow. Returns the function that stops it.
    dashboardEdgeScroll(scroller, pointerY, scrolled) {
        const zone = 48;
        let frame = 0;
        const tick = () => {
            const y = pointerY();
            if (scroller && y !== null) {
                const pane = scroller.getBoundingClientRect();
                const step = y < pane.top + zone ? -Math.ceil((pane.top + zone - y) / 3)
                    : y > pane.bottom - zone ? Math.ceil((y - (pane.bottom - zone)) / 3)
                    : 0;
                if (step !== 0) {
                    const before = scroller.scrollTop;
                    scroller.scrollTop += Math.max(-20, Math.min(20, step));
                    if (scroller.scrollTop !== before) {
                        scrolled();
                    }
                }
            }
            frame = requestAnimationFrame(tick);
        };
        frame = requestAnimationFrame(tick);
        return () => cancelAnimationFrame(frame);
    },

    // The dashboard grid (Home), and a card's own grid of parts: it tells the
    // page how many columns it has, and whether the window is a phone's (up to
    // 600 px, where the stylesheet's phone rules start: the dashboard's mobile
    // arrangement), now and whenever either changes — a phone turned included —
    // so the page places the cards for them; and in Edit mode it carries the
    // drag of an item. A press that moves less than a few pixels is a tap and selects;
    // past that the item follows the pointer, and where it would land is
    // outlined as it goes — in the success colour on free cells, in the error
    // colour where others stand, where letting go does nothing and the item
    // goes back (dashboardFree). On free cells the page is told the cell
    // (Home.MovedAsync) and draws it; the click that follows a drag is
    // swallowed so the item is not selected by the drop.
    // The dashboard's cards in sight and the page's own visibility, told to the
    // page (dashboardGrid, which owns it): a disconnect() is all it answers.
    dashboardSight(grid, page, signal) {
        const seen = new Set();
        let telling = 0;
        const tell = () => {
            telling = 0;
            page.invokeMethodAsync('VisibleChangedAsync', [...seen]);
        };
        const soon = () => {
            if (!telling) {
                telling = requestAnimationFrame(tell);
            }
        };
        // The pane that scrolls the dashboard is what a card is in sight of: a
        // margin on the window would not reach past the pane's own edge.
        const cards = new IntersectionObserver((entries) => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    seen.add(entry.target.dataset.id);
                } else {
                    seen.delete(entry.target.dataset.id);
                }
            }

            soon();
        }, { root: grid.closest('.wslc-fill-scroll'), rootMargin: '100%' });
        const watch = (node) => {
            if (node.classList?.contains('wslc-dash-item')) {
                cards.observe(node);
            }
        };
        grid.querySelectorAll('.wslc-dash-item').forEach(watch);
        const arranged = new MutationObserver((changes) => {
            for (const change of changes) {
                change.removedNodes.forEach((node) => {
                    if (node.dataset?.id) {
                        cards.unobserve(node);
                        seen.delete(node.dataset.id);
                        soon();
                    }
                });
                change.addedNodes.forEach(watch);
            }
        });
        arranged.observe(grid, { childList: true });

        const visible = () => page.invokeMethodAsync('PageVisibleChangedAsync', !document.hidden);
        document.addEventListener('visibilitychange', visible, { signal });
        if (document.hidden) {
            visible();
        }

        return {
            disconnect() {
                cards.disconnect();
                arranged.disconnect();
                cancelAnimationFrame(telling);
            },
        };
    },

    dashboardGrid(grid, page) {
        let columns = 0;
        let mobile = null;
        let settle = 0;
        const phone = window.matchMedia('(max-width: 600px)');
        // The dashboard is measured once before it is drawn, and once more when
        // a change of the window has ended, never while it is happening (the
        // owner, 22 September 2026: it opened as a desktop and turned into a
        // phone in front of the user). The first measure is told at once, so
        // the page knows which view it opens in; after that a change takes the
        // dashboard off the screen (Home.ResizingAsync) and only what the
        // window settles at — the mouse let go of the edge, the rotation
        // finished — is told, so the cards are placed and shown one time. A
        // card's own grid of parts has nothing to learn from this: it has the
        // card's columns in either view, and it is not the one that hides.
        const tell = () => {
            columns = window.wslcAgent.dashboardCell(grid).columns;
            mobile = phone.matches;
            page.invokeMethodAsync('ColumnsChangedAsync', columns, mobile);
        };
        const dashboard = grid.classList.contains('wslc-dash-grid');
        const measure = () => {
            if (window.wslcAgent.dashboardCell(grid).columns === columns && phone.matches === mobile) {
                return;
            }

            if (!dashboard || mobile === null) {
                tell();
                return;
            }

            page.invokeMethodAsync('ResizingAsync');
            clearTimeout(settle);
            settle = setTimeout(tell, window.wslcAgent.dashboardSettleMs);
        };
        const observer = new ResizeObserver(measure);
        // Every listener hangs on this, so dispose takes them all: a card's
        // parts grid is wired each time a part takes the focus, and listeners
        // left from an earlier time answered the same drop twice — the second
        // measured the part already back in place and sent it there (the
        // owner, 22 September 2026: a part moved once, never again).
        const listening = new AbortController();
        observer.observe(grid);
        measure();

        // What is in sight, for the dashboard alone (the owner, 24 September
        // 2026: only the cards that can be seen are brought up to date). The
        // cards on screen, or within a screen's reach of it so one scrolled to
        // is ready, are told to the page as they change (Home.VisibleChangedAsync),
        // once a frame however many moved; what is not told is neither read
        // from the agent nor drawn. Cards come and go as the dashboard is
        // arranged, so each one added is watched and each one taken off let go.
        // And whether the page itself can be seen — another tab, the app in the
        // background, the phone's screen off (Home.PageVisibleChangedAsync):
        // while it cannot, nothing is read or drawn at all.
        const sight = dashboard ? window.wslcAgent.dashboardSight(grid, page, listening.signal) : null;

        const ghost = document.createElement('div');
        ghost.className = 'wslc-dash-ghost';
        let drag = null;
        let swallowUntil = 0;

        const target = (item) => {
            const cell = window.wslcAgent.dashboardCell(grid);
            const rect = item.getBoundingClientRect();
            const origin = grid.getBoundingClientRect();
            const scale = window.wslcAgent.dashboardScale(grid);
            const reach = Math.max(columns, cell.tracks);
            const w = Math.min(Number(item.dataset.w), reach);
            return {
                x: Math.min(Math.max(0, Math.round((rect.left - origin.left) / scale / (cell.width + cell.gapX))), Math.max(0, reach - w)),
                y: Math.max(0, Math.round((rect.top - origin.top) / scale / (cell.height + cell.gapY))),
                w, h: Number(item.dataset.h),
            };
        };
        // Where the item would land, in the success colour on free cells and
        // in the error colour where others stand.
        const preview = (item) => {
            const at = target(item);
            ghost.classList.toggle('wslc-dash-ghost-blocked', !window.wslcAgent.dashboardFree(grid, item, at));
            ghost.style.gridArea = `${at.y + 1} / ${at.x + 1} / span ${at.h} / span ${at.w}`;
            if (!ghost.isConnected) {
                grid.appendChild(ghost);
            }
        };
        const scroller = window.wslcAgent.scrollParent(grid);
        // The item follows the pointer and whatever the pane has scrolled since
        // the drag began, so it stays under the hand while the pane moves.
        // How far down a dashboard card may be carried in one drag: its top to
        // just under the lowest edge on the dashboard, its own included — one
        // of its heights past where it is when it is the lowest card, where
        // the page will put it at the most. A card carried without a bound
        // stretched the pane it moved in, and the pane, scrolling itself at its
        // bottom edge, followed it for ever (the owner, 22 September 2026: a
        // card ran to row 810); bounded by the others alone, the lowest card
        // could not go down at all. Inside a card its parts are bounded by the
        // card itself.
        const floor = (item) => {
            if (!grid.classList.contains('wslc-dash-grid')) {
                return Infinity;
            }
            const cards = [...grid.children].filter((child) => child.dataset.id);
            const lowest = Math.max(0, ...cards.map((child) => child.offsetTop + child.offsetHeight));
            return Math.max(0, lowest + window.wslcAgent.dashboardCell(grid).gapY - item.offsetTop);
        };
        const follow = () => {
            if (!drag) {
                return;
            }
            const scrolled = scroller ? scroller.scrollTop - drag.scroll : 0;
            const scale = window.wslcAgent.dashboardScale(grid);
            const down = Math.min((drag.lastY - drag.y) / scale + scrolled, drag.floor);
            drag.item.style.transform = `translate(${(drag.lastX - drag.x) / scale}px, ${down}px)`;
            preview(drag.item);
        };
        const endDrag = (item) => {
            if (drag) {
                clearTimeout(drag.holding);
                if (drag.stopScroll) {
                    drag.stopScroll();
                }
            }
            item.style.transform = '';
            item.classList.remove('wslc-dash-dragging', 'mud-elevation-8');
            ghost.remove();
        };

        grid.addEventListener('pointerdown', (e) => {
            if (!grid.classList.contains('wslc-dash-editing') || e.button !== 0 || e.target.closest('.wslc-home-card-handle')) {
                return;
            }
            // This grid's own item under the pointer: the nearest item may be a
            // part inside a card, whose grid — when a part has the focus —
            // takes the press first and keeps it (stopPropagation), so a card
            // that has the focus is dragged by any point on it, parts included.
            let item = e.target.closest('.wslc-dash-item');
            while (item && item.parentElement !== grid) {
                item = item.parentElement.closest('.wslc-dash-item');
            }
            if (item) {
                e.stopPropagation();
                // A finger takes the item only once it has held still on it
                // (the owner, 22 September 2026): a swipe scrolls the dashboard,
                // as the emulator's mouse wheel does, which arrives as one. The
                // item lifts to say it is in the hand. A mouse takes it at once.
                const held = e.pointerType !== 'touch';
                const press = { item, pointer: e.pointerId, x: e.clientX, y: e.clientY, moving: false, held };
                if (!held) {
                    press.holding = setTimeout(() => {
                        if (drag === press && !press.moving) {
                            press.held = true;
                            item.classList.add('mud-elevation-8');
                            if (navigator.vibrate) {
                                navigator.vibrate(15);
                            }
                        }
                    }, window.wslcAgent.dashboardHoldMs);
                }
                drag = press;
            }
        }, { signal: listening.signal });
        grid.addEventListener('pointermove', (e) => {
            if (!drag || e.pointerId !== drag.pointer) {
                return;
            }
            drag.lastX = e.clientX;
            drag.lastY = e.clientY;
            if (!drag.moving) {
                if (Math.abs(e.clientX - drag.x) < 6 && Math.abs(e.clientY - drag.y) < 6) {
                    return;
                }
                if (!drag.held) {
                    endDrag(drag.item);
                    drag = null;
                    return;
                }
                drag.moving = true;
                drag.scroll = scroller ? scroller.scrollTop : 0;
                drag.floor = floor(drag.item);
                drag.stopScroll = window.wslcAgent.dashboardEdgeScroll(scroller, () => (drag ? drag.lastY : null), follow);
                drag.item.setPointerCapture(e.pointerId);
                drag.item.classList.add('wslc-dash-dragging', 'mud-elevation-8');
            }
            follow();
        }, { signal: listening.signal });
        grid.addEventListener('pointerup', (e) => {
            if (!drag || e.pointerId !== drag.pointer) {
                return;
            }
            const { item, moving } = drag;
            if (!moving) {
                endDrag(item);
                drag = null;
                return;
            }
            const at = target(item);
            endDrag(item);
            drag = null;
            swallowUntil = Date.now() + 300;
            // Where others stand, letting go does nothing: the item goes back.
            if (window.wslcAgent.dashboardFree(grid, item, at)) {
                page.invokeMethodAsync('MovedAsync', item.dataset.id, at.x, at.y);
            }
        }, { signal: listening.signal });
        grid.addEventListener('pointercancel', () => {
            if (drag) {
                endDrag(drag.item);
                drag = null;
            }
        }, { signal: listening.signal });
        // A drag that ends outside the grid still ends: the capture brings its
        // release here, and a lost capture cancels it — the dragged item's
        // alone. A finger's press is captured by what it touched, and handing
        // that capture to the item as the drag begins loses it there; that loss
        // bubbles here too, and taken for the item's it ended every touch drag
        // the moment it began (the owner, 22 September 2026: nothing moved on
        // the phone or the emulator).
        grid.addEventListener('lostpointercapture', (e) => {
            if (drag && drag.moving && e.target === drag.item) {
                endDrag(drag.item);
                drag = null;
            }
        }, { signal: listening.signal });
        grid.addEventListener('click', (e) => {
            if (Date.now() < swallowUntil) {
                e.stopPropagation();
                e.preventDefault();
            }
        }, { capture: true, signal: listening.signal });
        grid.addEventListener('touchmove', (e) => {
            if (drag && drag.held && e.cancelable) {
                e.preventDefault();
            }
        }, { passive: false, signal: listening.signal });
        grid.addEventListener('contextmenu', (e) => {
            if (grid.classList.contains('wslc-dash-editing')) {
                e.preventDefault();
            }
        }, { signal: listening.signal });
        // The wheel scrolls the dashboard and nothing else (the owner,
        // 22 September 2026): while an item is in the hand it does nothing, and
        // the pane scrolls only as the hand carries the item to its edge.
        window.addEventListener('wheel', (e) => {
            if (drag && drag.moving) {
                e.preventDefault();
            }
        }, { passive: false, signal: listening.signal });

        return {
            dispose() {
                observer.disconnect();
                sight?.disconnect();
                listening.abort();
                if (drag) {
                    endDrag(drag.item);
                    drag = null;
                }
                ghost.remove();
            },
        };
    },

    // Edit's one button on the dashboard (Home): a FAB the hand drags wherever
    // it wants it, remembered on this device (wslcAgent.dashboardFab).
    dashboardFab(el) {
        window.wslcAgent.floating(el, 'wslcAgent.dashboardFab', el);
    },

    // A card's or a part's Settings (the owner, 24 September 2026): a dialog
    // that does not hold the page, dragged by its title row and remembered on
    // this device (wslcAgent.settingsWindow), so it opens where it was last
    // left, off whatever it is changing. `inside` is any element in the
    // dialog; the dialog is found from it.
    movableDialog(inside) {
        const dialog = inside && inside.closest('.mud-dialog');
        const title = dialog && dialog.querySelector('.mud-dialog-title');
        if (dialog && title) {
            window.wslcAgent.floating(dialog, 'wslcAgent.settingsWindow', title);
        }
    },

    // Something that floats over the page where the hand puts it: `handle` is
    // what the hand takes it by (`el` itself for a button), and where it was
    // left is remembered on this device as a share of the window under `key`
    // in localStorage, so it comes back to the same corner at any window size.
    // A press that moves less than a few pixels is a tap and does its work;
    // past that it is a drag, and the click that would follow is swallowed.
    // On a handle that is not the element, a press on one of its buttons or
    // fields is theirs, not a drag.
    floating(el, key, handle) {
        const place = (fx, fy) => {
            const width = el.offsetWidth;
            const height = el.offsetHeight;
            const left = Math.min(Math.max(0, fx * (window.innerWidth - width)), window.innerWidth - width);
            const top = Math.min(Math.max(0, fy * (window.innerHeight - height)), window.innerHeight - height);
            el.style.position = 'fixed';
            el.style.margin = '0';
            el.style.left = `${left}px`;
            el.style.top = `${top}px`;
            el.style.right = 'auto';
            el.style.bottom = 'auto';
        };
        const stored = (() => {
            try {
                const value = localStorage.getItem(key);
                if (value) {
                    const [fx, fy] = value.split(',').map(Number);
                    return Number.isFinite(fx) && Number.isFinite(fy) ? { fx, fy } : null;
                }
            } catch {
                // Private windows: the button stays in its corner.
            }
            return null;
        })();
        if (stored) {
            place(stored.fx, stored.fy);
        }
        let drag = null;
        let swallowUntil = 0;
        handle.addEventListener('pointerdown', (e) => {
            if (e.button !== 0 || (handle !== el && e.target.closest('button, input, .mud-input-control'))) {
                return;
            }
            const rect = el.getBoundingClientRect();
            drag = { pointer: e.pointerId, x: e.clientX, y: e.clientY, left: rect.left, top: rect.top, moving: false };
        });
        handle.addEventListener('pointermove', (e) => {
            if (!drag || e.pointerId !== drag.pointer) {
                return;
            }
            const dx = e.clientX - drag.x;
            const dy = e.clientY - drag.y;
            if (!drag.moving) {
                if (Math.abs(dx) < 6 && Math.abs(dy) < 6) {
                    return;
                }
                drag.moving = true;
                handle.setPointerCapture(e.pointerId);
            }
            const fx = (drag.left + dx) / Math.max(1, window.innerWidth - el.offsetWidth);
            const fy = (drag.top + dy) / Math.max(1, window.innerHeight - el.offsetHeight);
            place(fx, fy);
        });
        const end = (e) => {
            if (!drag || e.pointerId !== drag.pointer) {
                return;
            }
            const { moving } = drag;
            drag = null;
            if (!moving) {
                return;
            }
            swallowUntil = Date.now() + 300;
            const rect = el.getBoundingClientRect();
            const fx = rect.left / Math.max(1, window.innerWidth - el.offsetWidth);
            const fy = rect.top / Math.max(1, window.innerHeight - el.offsetHeight);
            try {
                localStorage.setItem(key, `${fx},${fy}`);
            } catch {
                // Private windows: the place holds for this run.
            }
        };
        handle.addEventListener('pointerup', end);
        handle.addEventListener('pointercancel', end);
        handle.addEventListener('click', (e) => {
            if (Date.now() < swallowUntil) {
                e.stopPropagation();
                e.preventDefault();
            }
        }, true);
    },

    // The corner handle of the selected item — a card on the dashboard, or a
    // part inside a card in Layout: dragging it resizes the item cell by cell,
    // the cell read from the grid the item stands in (dashboardCell). The
    // size is previewed on the item while the pointer moves and told to the
    // component (`method` on `owner`: HomeCard.ResizedAsync for the card,
    // PartResizedAsync for a part) when it lets go, which then draws the size
    // it settled on. The handle stops its press from reaching the grid, so the
    // item under it is not dragged instead.
    dashboardResize(handle, owner, method) {
        const item = handle.closest('.wslc-dash-item');
        const grid = item && item.parentElement;
        if (!item || !grid) {
            return;
        }
        // The pane the grid scrolls in: a card made taller than the room below
        // the pointer would need the hand to leave the window, so the pane
        // scrolls by itself near its edges and what it scrolls counts as drag.
        const scroller = window.wslcAgent.scrollParent(grid);
        let start = null;
        // The grid says what bounds it has of its own (data-max-columns,
        // data-max-rows: a card's, for the parts inside it); the dashboard
        // has none, and the only bound is then the columns there are, which
        // is the room, not a rule. A card grown past the right edge is pulled
        // left, as it will be when the hand lets go.
        const spans = (e) => {
            const cell = window.wslcAgent.dashboardCell(grid);
            const columnsThere = Math.max(cell.columns, cell.tracks) || 1;
            const maxColumns = Math.min(Number(grid.dataset.maxColumns) || columnsThere, columnsThere);
            const maxRows = Number(grid.dataset.maxRows) || Number.MAX_SAFE_INTEGER;
            const scrolled = scroller ? scroller.scrollTop - start.scrollTop : 0;
            const span = (length, size, gap, max) => Math.min(max, Math.max(1, Math.round((length + gap) / (size + gap))));
            const scale = window.wslcAgent.dashboardScale(grid);
            const columns = span(start.width + (e.clientX - start.x) / scale, cell.width || item.offsetWidth, cell.gapX, maxColumns);
            return {
                columns,
                rows: span(start.height + (e.clientY - start.y) / scale + scrolled, cell.height || item.offsetHeight, cell.gapY, maxRows),
                x: Math.min(Number(item.dataset.x), Math.max(0, columnsThere - columns)),
            };
        };
        // While the pointer is down the item itself does not change (the
        // owner, 22 September 2026: growing it as the hand moved changed what
        // the hand was measured against, and it shook): the size it would
        // take is drawn as an outline over its cells, and the item takes it
        // when the hand lets go.
        const ghost = document.createElement('div');
        ghost.className = 'wslc-dash-ghost';
        // The outline of the size the pointer asks for, from where it is now.
        const show = () => {
            const { columns, rows, x } = spans(start.last);
            const area = { x, y: Number(item.dataset.y), w: columns, h: rows };
            ghost.classList.toggle('wslc-dash-ghost-blocked', !window.wslcAgent.dashboardFree(grid, item, area));
            ghost.style.gridArea = `${area.y + 1} / ${x + 1} / span ${rows} / span ${columns}`;
            if (!ghost.isConnected) {
                grid.appendChild(ghost);
            }
        };
        // The wheel scrolls the dashboard, never the size in the hand.
        const stillWheel = (e) => e.preventDefault();
        // The corner in the hand: the pane scrolls at its edges from here on.
        const take = (pointer) => {
            start.held = true;
            handle.setPointerCapture(pointer);
            start.stopScroll = window.wslcAgent.dashboardEdgeScroll(scroller, () => (start ? start.last.clientY : null), () => start && start.moving && show());
        };
        handle.addEventListener('pointerdown', (e) => {
            e.stopPropagation();
            window.addEventListener('wheel', stillWheel, { passive: false });
            // The item's size as its cells are measured: in layout pixels.
            start = {
                x: e.clientX, y: e.clientY, width: item.offsetWidth, height: item.offsetHeight,
                scrollTop: scroller ? scroller.scrollTop : 0,
                last: { clientX: e.clientX, clientY: e.clientY },
                held: false,
                moving: false,
            };
            if (e.pointerType !== 'touch') {
                e.preventDefault();
                take(e.pointerId);
                return;
            }
            // A finger holds the corner still first; a swipe from it scrolls.
            const press = start;
            press.holding = setTimeout(() => {
                if (start === press) {
                    take(e.pointerId);
                    if (navigator.vibrate) {
                        navigator.vibrate(15);
                    }
                }
            }, window.wslcAgent.dashboardHoldMs);
        });
        handle.addEventListener('pointermove', (e) => {
            if (!start) {
                return;
            }
            if (!start.held) {
                if (Math.abs(e.clientX - start.x) >= 6 || Math.abs(e.clientY - start.y) >= 6) {
                    end();
                }
                return;
            }
            start.moving = true;
            start.last = { clientX: e.clientX, clientY: e.clientY };
            show();
        });
        handle.addEventListener('touchmove', (e) => {
            if (start && start.held && e.cancelable) {
                e.preventDefault();
            }
        }, { passive: false });
        const end = () => {
            if (start) {
                clearTimeout(start.holding);
                if (start.stopScroll) {
                    start.stopScroll();
                }
            }
            window.removeEventListener('wheel', stillWheel);
            ghost.remove();
            start = null;
        };
        handle.addEventListener('pointerup', (e) => {
            if (!start) {
                return;
            }
            if (!start.moving) {
                end();
                return;
            }
            const { columns, rows, x } = spans(e);
            end();
            // Where others stand, letting go does nothing: the item keeps its size.
            if (window.wslcAgent.dashboardFree(grid, item, { x, y: Number(item.dataset.y), w: columns, h: rows })) {
                owner.invokeMethodAsync(method || 'ResizedAsync', columns, rows);
            }
        });
        handle.addEventListener('pointercancel', () => {
            if (start) {
                end();
            }
        });
    },

    // Light or dark, as this device last had it: the theme belongs to the client,
    // not to the agent, so two clients of the same agent each keep their own and
    // the app opens in the theme it was left in. Without an argument it only
    // answers the one stored.
    theme(dark) {
        try {
            if (dark !== undefined && dark !== null) {
                localStorage.setItem('wslcAgent.theme', dark ? 'dark' : 'light');
            }

            return localStorage.getItem('wslcAgent.theme') === 'dark';
        } catch {
            // Private windows: the theme still turns over, it is just not remembered.
            return false;
        }
    },

    // The look chosen in Settings (Layout), as this device keeps it: the same
    // kind of setting as the theme and the page zoom, so it belongs to the
    // client and not to the agent. Without an argument it only answers what is
    // stored.
    style(json) {
        try {
            if (json !== undefined && json !== null) {
                localStorage.setItem('wslcAgent.style', json);
            }

            return localStorage.getItem('wslcAgent.style');
        } catch {
            // Private windows: the look still applies, it is just not remembered.
            return null;
        }
    },

    // What the stylesheet reads: one custom property per value, on the document,
    // so applying a look is one write and no screen has to be drawn again.
    // A file out of the browser, for the one thing the application has no other
    // way to hand over: a look, so it can be carried to another machine.
    download(name, text, type) {
        const blob = new Blob([text], { type: type || 'application/json' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = name;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    },

    // The look the application ships with: a file beside this one, read once at
    // startup. It is what the product looks like, so it is asked for before the
    // device's own choices and overridden by them.
    async styleFile() {
        try {
            const response = await fetch('/_content/WslcAgent.UI/wslc-style.json', { cache: 'no-cache' });
            return response.ok ? await response.text() : null;
        } catch {
            // Offline or missing: the values compiled in are the same ones.
            return null;
        }
    },

    // A look applied but not saved, kept for this tab alone: reloading the page
    // finds it still on, closing the application forgets it and what was saved
    // comes back. An empty string clears it, which is what saving does.
    styleTried(json) {
        try {
            if (json !== undefined && json !== null) {
                if (json === '') {
                    sessionStorage.removeItem('wslcAgent.styleTried');
                } else {
                    sessionStorage.setItem('wslcAgent.styleTried', json);
                }
            }

            return sessionStorage.getItem('wslcAgent.styleTried');
        } catch {
            return null;
        }
    },

    // What is not a length: a name written on the document, for the rules that
    // cannot be a number — how a button is filled, for one.
    styleFlags(flags) {
        for (const [name, value] of Object.entries(flags || {})) {
            document.documentElement.setAttribute(name, value);
        }
    },

    styleVars(values) {
        for (const [name, value] of Object.entries(values || {})) {
            if (value === '') {
                // Nothing chosen for it: the theme's own value comes back.
                document.documentElement.style.removeProperty(name);
            } else {
                document.documentElement.style.setProperty(name, value);
            }
        }
    },
    // With `follow`, a box the reader has scrolled up in is left where it is,
    // as the reference's pull console does; scrolling back down resumes it.
    // (The Logs page does not use it: it holds its list while the reader is on
    // it, and otherwise goes to the end every time.)
    // A grid is scrolled by the table MudBlazor puts inside it, so the box
    // named is a grid's wrapper as often as the scrolling box itself.
    scrollToEnd(id, follow = false) {
        const named = document.getElementById(id);
        if (!named) {
            return;
        }

        const element = named.querySelector(".mud-table-container") ?? named;

        if (follow && !element.dataset.followWatched) {
            element.dataset.followWatched = "true";
            element.dataset.atEnd = "true";
            element.addEventListener("scroll", () => {
                element.dataset.atEnd = String(element.scrollHeight - element.scrollTop - element.clientHeight < 40);
            });
        }

        if (!follow || element.dataset.atEnd === "true") {
            // Once now, and again on the next two frames: a box of panels settles
            // its height a frame or two after Blazor has drawn it, and a single
            // scroll measured mid-layout landed a panel short of the end.
            const toEnd = () => { element.scrollTop = element.scrollHeight; };
            toEnd();
            requestAnimationFrame(() => { toEnd(); requestAnimationFrame(toEnd); });
        }
    },

    // Files into a container, chosen and sent by the browser itself, in two
    // steps so the view can say what is going while it goes: pickFiles opens
    // the rail's own input and answers what was chosen, sendFile posts one of
    // them. Several chosen at once are several sends, and the agent keeps one
    // job for each.
    //
    // Why the browser and not .NET: in WebAssembly a request cannot be
    // streamed — the browser only takes a streaming body over HTTP/2 with
    // duplex, and the agent serves HTTP/1.1 — so .NET would have to hold the
    // whole file in memory, twice, before the first byte left. A backup of a
    // container is gigabytes. Handed to fetch, the browser reads it from the
    // disk as it sends it and costs nothing, which is how the reference
    // project sent files and why it never had a size limit.
    //
    // No progress here: the agent counts what it receives, and the ring on the
    // container's row is that count (GET /containers/uploads).
    // Two ways of asking for a file, because Chromium has two and only one of
    // them is reliable here. showOpenFilePicker (the File System Access API)
    // does not go through the element at all; the element's own chooser is
    // what stops answering — it takes a selection and does nothing with Open,
    // again and again, until the page is reloaded (seen on Windows 11,
    // 23 September 2026, with the picker alive and waiting on our side: the
    // dialog answered Escape and refused Open). The API also opens where it
    // was left, per id, which the element's chooser cannot be told.
    //
    // It needs a secure context, which the agent's own machine is (localhost)
    // and a WebView is, but a plain http page on another machine is not, and
    // Firefox has neither the API nor a plan for one. The input stays for all
    // of those, and for anything the API refuses.
    async pickFiles(input) {
        if (window.showOpenFilePicker) {
            try {
                const handles = await window.showOpenFilePicker({ multiple: true, id: 'wslcAgentUpload' });
                return wslcAgentPark(await Promise.all(handles.map(handle => handle.getFile())));
            } catch (failure) {
                if (failure && failure.name === 'AbortError') {
                    // Closed with nothing chosen, which is an answer.
                    return wslcAgentPark([]);
                }
                // A browser that has the API and will not use it here: the
                // element's chooser is still better than no picker at all.
            }
        }

        return new Promise(resolve => {
            // The same file twice in a row still raises change: the value is
            // cleared before the picker opens, not after it answers.
            input.value = '';
            const done = chosen => {
                input.onchange = null;
                input.oncancel = null;
                resolve(wslcAgentPark(chosen));
            };
            input.onchange = () => done(Array.from(input.files || []));
            // A picker closed with nothing chosen: without this the caller
            // would wait for an answer that never comes.
            input.oncancel = () => done([]);
            input.click();
        });
    },

    // One of the files a ticket is holding, to the agent. The request lives in
    // the page, not in the screen that asked for it: closing the Files view
    // does not touch it. The answer is the agent's, whole, so the caller can
    // show what it said when it refused.
    async sendFile(ticket, index, url, token) {
        const files = wslcAgentPicked.get(ticket) || [];
        const file = files[index];
        // Its place is emptied, not removed: the others keep their own. The
        // ticket goes when the last of its files has gone.
        files[index] = null;
        if (files.every(waiting => !waiting)) {
            wslcAgentPicked.delete(ticket);
        }

        if (!file) {
            return { ok: false, status: 0, error: 'The browser no longer has that file to send' };
        }

        const body = new FormData();
        body.append('file', file, file.name);
        try {
            const response = await fetch(url, {
                method: 'POST',
                body,
                credentials: 'include',
                headers: token ? { Authorization: 'Bearer ' + token } : {},
            });
            if (response.ok) {
                return { ok: true, status: response.status, error: '' };
            }

            const problem = await response.json().catch(() => null);
            return { ok: false, status: response.status, error: (problem && (problem.detail || problem.title)) || response.statusText };
        } catch (failure) {
            // The agent went, the network went, or the request was aborted.
            return { ok: false, status: 0, error: String((failure && failure.message) || failure) };
        }
    },

    // A finger is the primary pointer: a phone or a tablet, where the thumb
    // reaches the right edge sooner than the left. A mouse, however small the
    // window, is not.
    isHandheld() {
        return window.matchMedia('(pointer: coarse)').matches;
    },

    // Back, in the order the reference's client walks it: whatever is over the
    // page first, then the navigation that opened over it, and only then the
    // step before. False when there is nothing left, which is what lets the
    // native client leave the app instead of swallowing the gesture.
    //
    // The native client asks for this because its web view has no history of
    // its own to walk: the UI navigates inside one document, so its back stack
    // lives here, not in the view.
    back() {
        const dialog = document.querySelector('.mud-dialog-container .mud-dialog');
        if (dialog) {
            // What a dialog does with Escape is the dialog's business: one that
            // holds unsaved work asks before it goes.
            document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
            return true;
        }

        // The navigation drawer of a narrow window is an overlay over the page,
        // and its overlay is what closes it.
        const overlay = document.querySelector('.mud-overlay-drawer, .mud-drawer-overlay, .mud-overlay');
        if (overlay) {
            overlay.click();
            return true;
        }

        if (window.history.length > 1) {
            window.history.back();
            return true;
        }

        return false;
    },

    // Which native client this browser's device takes: "android", "windows"
    // or "ios" (none yet). Client hints first, since a desktop-site Chrome on
    // a phone spoofs a desktop user agent; iPadOS in desktop mode looks like a
    // Mac and must not be offered the Windows installer.
    clientPlatform() {
        if (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) {
            return 'ios';
        }
        const hints = navigator.userAgentData;
        if (hints) {
            const hinted = String(hints.platform || '').toLowerCase();
            if (hinted === 'android' || hinted === 'ios') {
                return hinted;
            }
            if (hints.mobile && hinted !== 'windows' && hinted !== 'macos') {
                return 'android';
            }
        }
        const ua = navigator.userAgent || '';
        if (/Android/i.test(ua)) {
            return 'android';
        }
        if (/iPhone|iPad|iPod/i.test(ua)) {
            return 'ios';
        }
        const platform = String(navigator.platform || '');
        if (/linux/i.test(platform) && (/arm|aarch64/i.test(platform) || navigator.maxTouchPoints > 1)) {
            return 'android';
        }
        return 'windows';
    },
};

// A page's verb bar (the row under the title bar) never wraps and never loses a
// verb. When the room runs short it gives things up one at a time, in the order
// the owner set: the texts of the stats at the left first, then the words on
// the filter switches, then the size of the tools, and only at the very end the
// words on the buttons — a verb that cannot be read is the last thing to lose.
//
// Every bar measures what it needs against the room it was given, so this is
// not a phone-or-desktop switch: a page with one verb keeps everything at a
// width where a page with six cannot, and the same page keeps it in a wide
// window and gives it up in a narrow one. CSS cannot ask whether its content
// fits, which is why the measuring is here and the steps are there (the
// .wslc-fit-* rules of wslc-agent-ui.css).
// The texts a bar gives up when the room runs short, in the owner's order: the
// stats at the left first, then the words on the filter switches (their initial
// and their tooltip stay), and the words on the buttons last of all.
const wslcFitSteps = {
    // The page's verb bar: the stats at the left, then the switches' words.
    '.wslc-toolbar': ['small', 'tight', 'letters', 'icons'],
    // The title bar: the search box gives up width first — it is a box, not a
    // word — then the application's name over the session line (the session
    // line stays: it is what the bar is for), then the words on the buttons.
    // The zoom stays at every width (the owner, 20 September 2026).
    '.wslc-appbar-row': ['search', 'brand', 'icons'],
    // The navigation button's row of sections (the owner, 20 September 2026):
    // the air between the buttons goes first, then the words drop to the
    // theme's smallest size, then the row closes up, and only when that is not
    // enough the words go and the glyphs stay.
    '.wslc-verbs-nav': ['tight', 'small', 'icons'],
};

const wslcFitKinds = Object.keys(wslcFitSteps);

// The title of the title bar asks for its whole name — the page's title was
// reading "Contai…" while the search box beside it kept 14rem — but never for
// more than this, so that a container called after its whole registry path does
// not drag the bar down a step on its own. Past that it ends in an ellipsis, as
// it always has. The brand block beside it is counted whole.
const wslcFitGives = '.wslc-appbar-start';
const wslcFitGivesTitle = '.wslc-title';
const wslcFitGivesMost = 14 * 16;

// Between the switches' words and the buttons' words the bar closes up instead:
// it shrinks by exactly what is missing, never past this much of its size. Five
// pixels short is not a reason to take a verb's word away.
const wslcFitFloor = 0.8;

// The steps that make a text smaller and take nothing away.
const wslcFitShrinks = new Set(['small']);

const wslcFitObserved = new WeakSet();
let wslcFitQueued = false;

// The steps this kind of bar has to give, in order.
function wslcStepsOf(bar) {
    const kind = wslcFitKinds.find(one => bar.matches(one));
    return kind ? wslcFitSteps[kind] : [];
}

// The bar as it stands after the first `taken` steps.
function wslcFitStep(bar, taken) {
    wslcStepsOf(bar).forEach((step, index) => bar.classList.toggle('wslc-fit-' + step, index < taken));
}

// 1 is the bar as it stands on a monitor.
function wslcFitScale(bar, scale) {
    bar.style.setProperty('--wslc-fit', String(scale));
}

// What the bar would need with nothing squeezed: each child measured at its own
// natural width, one at a time, while the bar keeps the width it has. The bar
// is a container-query container, so widening it to measure would answer with
// the type sizes of a bar that wide.
function wslcBarNeeds(bar) {
    const style = getComputedStyle(bar);
    const gap = parseFloat(style.columnGap) || 0;
    let need = 0;
    let shown = 0;
    for (const child of bar.children) {
        if (getComputedStyle(child).display === 'none') {
            continue;
        }

        child.classList.add('wslc-measure');
        // Layout pixels, as the room is: the app is zoomed as a browser zooms
        // it, and a bounding rectangle answers in screen pixels — at 150 % a
        // row measured that way seemed half again as wide as its room and the
        // steps came out wrong (the owner, 20 September 2026).
        const natural = child.offsetWidth;
        child.classList.remove('wslc-measure');
        if (child.matches(wslcFitGives)) {
            const title = child.querySelector(wslcFitGivesTitle);
            const titleWidth = title ? title.offsetWidth : 0;
            need += natural - titleWidth + Math.min(titleWidth, wslcFitGivesMost);
        } else {
            need += natural;
        }
        shown += 1;
    }

    return need + Math.max(0, shown - 1) * gap;
}

// The gaps between the bar's children: the bar's own, which the zoom on the
// children does not make smaller.
function wslcBarGaps(bar) {
    const shown = [...bar.children].filter(child => getComputedStyle(child).display !== 'none').length;
    return Math.max(0, shown - 1) * (parseFloat(getComputedStyle(bar).columnGap) || 0);
}

// How much of its size the bar keeps to fit its room, never below the floor.
// Only the children are zoomed, so only what the gaps leave is shared out:
// divided whole, the gaps included, the bar came out a few pixels short every
// time and a text went that a closer bar would have kept (the owner,
// 24 September 2026).
function wslcCloseUp(bar, room) {
    const gaps = wslcBarGaps(bar);
    // What the bar draws as well as what its children measure: a pixel or two
    // the measure does not see (a rounding, a line under a line) left the bar
    // over by that much with a scale above 1, which changes nothing.
    const style = getComputedStyle(bar);
    const drawn = bar.scrollWidth - (parseFloat(style.paddingLeft) || 0) - (parseFloat(style.paddingRight) || 0);
    const need = Math.max(wslcBarNeeds(bar), drawn);
    return Math.min(1, Math.max(wslcFitFloor, (room - gaps) / Math.max(1, need - gaps)));
}

// Both the sum and the bar itself have to agree: the sum says what the children
// need, the bar says what they took.
function wslcBarFits(bar, room) {
    return wslcBarNeeds(bar) <= room + 1 && bar.scrollWidth <= bar.clientWidth + 1;
}

// A bar is measured again only when its room or what it holds has changed.
// Each pass takes the bar back to its full size to measure it, and for that
// moment a list under it is shorter: every refresh of the Logs page moved both
// lists' scroll 3 px and back, over and over (the owner, 24 September 2026,
// on the emulator: "sube y baja").
function wslcFitBar(bar) {
    const style = getComputedStyle(bar);
    const room = bar.clientWidth - (parseFloat(style.paddingLeft) || 0) - (parseFloat(style.paddingRight) || 0);
    const key = `${room}|${wslcFitText(bar)}|${bar.getElementsByTagName('*').length}`;
    if (bar.dataset.fitKey === key) {
        return;
    }

    // The bar keeps its height while it is measured: taken back to its full
    // size for a moment it grew a pixel or two, the pane under it shrank, and
    // a list at its end had its scroll cut and was sent back down by its
    // Autorefresh — with both of the Logs page's on, every entry that changed
    // the log's count moved both lists (the owner, 24 September 2026).
    const height = bar.style.height;
    bar.style.height = `${bar.offsetHeight}px`;
    try {
        wslcFitBarAt(bar, room);
    } finally {
        bar.style.height = height;
    }

    bar.dataset.fitKey = key;
}

// What a bar shows, for the key it is measured again by. Once its stats' texts
// are gone (tight) theirs is left out: the log's count changes with every
// entry, and each change had the bar show it to measure it, find it did not
// fit and hide it again — the log moved under the reader while Activity,
// whose count stands still at its 200, did not (the owner, 24 September
// 2026). A change of room still measures it again, and gives the texts back
// where they fit.
function wslcFitText(bar) {
    const stats = bar.classList.contains('wslc-fit-tight') ? bar.querySelector(':scope > .wslc-stats') : null;
    return stats ? bar.textContent.replace(stats.textContent, '') : bar.textContent;
}

function wslcFitBarAt(bar, room) {
    if (room <= 0) {
        // Not on screen (a tab that is not the open one): measured now it would
        // answer zero, and a bar nobody is looking at would be stripped.
        return;
    }

    // Every pass starts from the top — the bar whole, at the size it has on a
    // monitor — and comes down from there. A phone turned on its side has room
    // again, and what it gave up when it was upright it takes straight back.
    for (let taken = 0; taken < wslcStepsOf(bar).length; taken += 1) {
        wslcFitScale(bar, 1);
        wslcFitStep(bar, taken);
        if (wslcBarFits(bar, room)) {
            return;
        }

        if (taken + 1 === wslcStepsOf(bar).length) {
            // The next step would take the words off the buttons. Before that,
            // the bar closes up by what is missing and no more.
            break;
        }

        // A step that only makes a text smaller is tried with the bar closed up
        // too, before the next step takes a text away: a smaller word is still
        // read, one that is gone is not (the owner, 24 September 2026).
        if (taken > 0 && wslcFitShrinks.has(wslcStepsOf(bar)[taken - 1])) {
            wslcFitScale(bar, wslcCloseUp(bar, room));
            if (wslcBarFits(bar, room)) {
                return;
            }
        }
    }

    if (wslcBarFits(bar, room)) {
        return;
    }

    wslcFitScale(bar, wslcCloseUp(bar, room));
    if (wslcBarFits(bar, room)) {
        return;
    }

    // Not even closed up to the floor: the words go, back at full size, and the
    // bar closes up again only if it still has to.
    wslcFitScale(bar, 1);
    wslcFitStep(bar, wslcStepsOf(bar).length);
    if (!wslcBarFits(bar, room)) {
        wslcFitScale(bar, wslcCloseUp(bar, room));
    }
}

// The page's verbs over the navigation button stand one over the other and
// grow upwards, and on a short window the top ones went past its edge (the
// owner, 29 September 2026). The column measures itself against the window's
// height as a bar does against its width, and gives up in the owner's order:
// the air between the verbs, then their size — MudBlazor's small FAB and the
// theme's smallest words — and at the last it closes up by what is missing.
// A verb keeps its word: it is what says what it does.
const wslcVerbsSteps = ['tight', 'small'];

// How close to the window's top the highest verb may stand.
const wslcVerbsMargin = 8;

// Closed up no further than this: past it a word is no longer read.
const wslcVerbsFloor = 0.5;

function wslcVerbsFit(menu) {
    const key = `${window.innerHeight}|${menu.getElementsByTagName('*').length}`;
    if (menu.dataset.fitKey === key) {
        return;
    }

    const over = () => wslcVerbsMargin - menu.getBoundingClientRect().top;
    menu.style.setProperty('--wslc-fit-verbs', '1');
    for (let taken = 0; taken <= wslcVerbsSteps.length; taken += 1) {
        wslcVerbsSteps.forEach((step, index) => menu.classList.toggle('wslc-fit-' + step, index < taken));
        if (over() <= 0) {
            menu.dataset.fitKey = key;
            return;
        }
    }

    // Still past the top at the smallest step: the verbs close up by what
    // they go past it, the row of sections under them left as it is.
    const lists = [...menu.querySelectorAll(':scope > .wslc-verbs-list')];
    const tall = lists.reduce((sum, list) => sum + list.getBoundingClientRect().height, 0);
    if (tall > 0) {
        menu.style.setProperty('--wslc-fit-verbs', String(Math.max(wslcVerbsFloor, (tall - over()) / tall)));
    }

    menu.dataset.fitKey = key;
}

// A list keeps its identity columns frozen at the left and its actions at the
// right, so neither leaves the screen while the row is read sideways. On a
// phone that block can take nearly the whole width — the checkbox, the state,
// the name and the actions left 93 px of a 395 px screen to scroll through
// nine columns — and a table that cannot be read sideways is not a table. So
// when the frozen block takes more than this much of the width, the name is let
// go: the checkbox and the state stay at the left, the actions stay at the
// right, and everything else scrolls under them.
const wslcFrozenMost = 0.6;

function wslcFreezeFit(grid) {
    const head = grid.querySelector('thead tr');
    const room = grid.clientWidth;
    if (!head || room <= 0) {
        // Between two paints a list can measure nothing at all. Letting the
        // columns go at that moment and leaving them there is how a table ended
        // up with four frozen columns out of eight: what it had stays until
        // there is something to measure.
        return;
    }

    // Measured as it stands whole, so a list that gets its room back — a phone
    // turned on its side, a window widened — freezes the name again.
    grid.classList.remove('wslc-thaw');

    let frozen = 0;
    for (const cell of head.children) {
        if (cell.classList.contains('sticky-left') || cell.classList.contains('sticky-right')) {
            frozen += cell.getBoundingClientRect().width;
        }
    }

    if (frozen > room * wslcFrozenMost) {
        grid.classList.add('wslc-thaw');
    }
}

function wslcFitBars() {
    wslcFitQueued = false;
    for (const bar of document.querySelectorAll(wslcFitKinds.join(', '))) {
        if (!wslcFitObserved.has(bar)) {
            wslcFitObserved.add(bar);
            wslcFitRoom.observe(bar);
        }

        wslcFitBar(bar);
    }

    for (const menu of document.querySelectorAll('.wslc-verbs-menu')) {
        wslcVerbsFit(menu);
    }

    for (const grid of document.querySelectorAll('.wslc-fill')) {
        if (!wslcFitObserved.has(grid)) {
            wslcFitObserved.add(grid);
            wslcFitRoom.observe(grid);
        }

        wslcFreezeFit(grid);
        wslcTableFloor(grid);
    }
}

// The narrowest a table may be: every column as it stands and the one that
// takes the spare width at its declared minimum, which a fixed layout does not
// read on its own — on a phone that column was left 0 px wide. Written on the
// list's box, where Blazor writes no style, for the stylesheet to use.
function wslcTableFloor(grid) {
    const head = grid.querySelector(':scope > .mud-table table')?.tHead;
    if (!head || grid.clientWidth <= 0) {
        return;
    }

    const cells = [...head.rows].reduce((most, row) => (row.cells.length > most.length ? [...row.cells] : most), []);
    const floor = cells.reduce((sum, cell) => sum + (cell.classList.contains('wslc-col-stretch')
        ? parseFloat(getComputedStyle(cell).minWidth) || 0
        : cell.getBoundingClientRect().width), 0);
    grid.style.setProperty('--wslc-table-min', `${Math.ceil(floor)}px`);
}

function wslcFitSoon() {
    if (!wslcFitQueued) {
        wslcFitQueued = true;
        requestAnimationFrame(wslcFitBars);
    }
}

// The room changes (the window, the sidebar, a phone turning over) and so does
// what the bar holds (a value grows a digit, a verb appears with a selection):
// both send the bars back to the tape measure. Attribute changes are left out
// on purpose — the step is written as one, and watching it would chase its own
// tail.
const wslcFitRoom = new ResizeObserver(wslcFitSoon);
// The verbs' column is measured against the window's height, which no
// element's size reports.
window.addEventListener('resize', wslcFitSoon);
// A table's rows are not a bar's business: they change on every refresh and
// fit nothing (the header row, which a grid's floor is measured from, is not
// in the body).
new MutationObserver(changes => {
    if (changes.some(change => !(change.target instanceof Element ? change.target : change.target.parentElement)?.closest('tbody'))) {
        wslcFitSoon();
    }
}).observe(document.documentElement, {
    childList: true,
    subtree: true,
    characterData: true,
});
wslcFitSoon();

// Double-click a column's resize handle and the column goes back to the width
// the application gives it: dropping the width the drag wrote inline leaves the
// stylesheet's own, which is the default every table starts at.
document.addEventListener('dblclick', event => {
    const resizer = event.target.closest('.wslc-fill .mud-resizer');
    if (resizer) {
        resizer.closest('th').style.width = '';
    }
});

// A press on a column's resize handle is a resize and never the start of the
// header's drag to reorder the columns. The browser began that drag the moment
// the handle was pressed, cancelled the pointer (pointercancel) and took the
// resize's capture with it, so no column could be made wider or narrower —
// the drag moved it instead (the owner, 24 September 2026; measured on the
// Logs page: dragstart 1 ms after the press, the capture lost 5 ms later).
// A drag from anywhere else on the header still reorders.
// An opened row's detail is one cell MudBlazor spans over "1000" columns. In
// the fixed layout the tables use, those are a thousand phantom columns that
// share the spare width with the one column meant to hold it — the log's
// Message, Activity's Command — and leave it one pixel wide the moment a row
// opens (the owner, 24 September 2026; measured: Message 869 px closed, 1 px
// open). The cell spans the columns the table has, once a frame at most.
let wslcSpanQueued = false;
function wslcSpanDetails() {
    wslcSpanQueued = false;
    for (const cell of document.querySelectorAll('.wslc-fill td.mud-table-child-content[colspan="1000"]')) {
        const head = cell.closest('table')?.tHead;
        const columns = head ? Math.max(0, ...[...head.rows].map(row => row.cells.length)) : 0;
        if (columns > 0) {
            cell.colSpan = columns;
        }
    }
}

new MutationObserver(() => {
    if (!wslcSpanQueued) {
        wslcSpanQueued = true;
        requestAnimationFrame(wslcSpanDetails);
    }
}).observe(document.documentElement, { childList: true, subtree: true });

let wslcResizing = false;
document.addEventListener('pointerdown', event => {
    wslcResizing = event.target instanceof Element && event.target.closest('.wslc-fill .mud-resizer') !== null;
}, true);
document.addEventListener('dragstart', event => {
    if (wslcResizing) {
        event.preventDefault();
    }
}, true);
for (const end of ['pointerup', 'pointercancel']) {
    document.addEventListener(end, () => {
        if (wslcResizing) {
            // A column changed width: the table's floor with it.
            wslcFitSoon();
        }

        wslcResizing = false;
    }, true);
}

// The zoom this device chose, read before the first paint so the page does not
// jump from 100 % to it once Blazor starts.
function wslcAgentStoredZoom() {
    try {
        return localStorage.getItem('wslcAgent.zoom');
    } catch {
        return null;
    }
}

window.wslcAgent.zoom();
