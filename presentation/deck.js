// Freitty Cabinet API — slide deck. No dependencies.
// The current slide lives in the URL hash (#/6), so a link opens a slide and Back/Forward move between slides.

(() => {
  "use strict";

  const STAGE_WIDTH = 1600;
  const STAGE_HEIGHT = 900;
  const FLOW_QUERY = "(max-width: 760px), (max-height: 430px)";

  const stage = document.getElementById("stage");
  const slides = Array.from(stage.querySelectorAll(":scope > .slide"));
  const sectionNames = stage.dataset.sections.split("|");
  const status = document.getElementById("deck-status");
  const overview = document.getElementById("overview");
  const prevButton = document.getElementById("prev");
  const nextButton = document.getElementById("next");
  const overviewButton = document.getElementById("open-overview");
  const flowMedia = window.matchMedia(FLOW_QUERY);

  let current = -1;
  let isFlow = false;

  // ---------- Highlighting ----------

  const CS_KEYWORDS = new Set((
    "abstract as async await base bool break case catch char class const continue decimal default delegate do " +
    "double else enum event false finally float for foreach get if in init int interface internal is long " +
    "namespace new not null object or and out override params partial private protected public readonly record " +
    "ref required return sealed set static string struct switch this throw true try typeof using var virtual " +
    "void when where while with yield"
  ).split(" "));

  const SQL_KEYWORDS = new Set((
    "select from where and or in union inner join on order by desc asc limit offset exists as ilike escape group " +
    "count not null is update set insert into delete with filter case when then else end distinct over partition " +
    "row_number"
  ).split(" "));

  const SH_KEYWORDS = new Set("if then else fi for do done return local echo case esac in".split(" "));

  const PATTERNS = {
    cs: /(\/\/.*$)|(\$?"(?:[^"\\]|\\.)*"?)|('(?:[^'\\]|\\.)')|(\b\d[\d_]*(?:\.\d+)?[mMfFdDlL]?\b)|([A-Za-z_][A-Za-z0-9_]*)|(…)/g,
    sql: /(--.*$)|('(?:[^']|'')*'?)|(\b\d+(?:\.\d+)?\b)|([A-Za-z_][A-Za-z0-9_]*)|(…)/g,
    json: /("(?:[^"\\]|\\.)*")(\s*:)?|(\b-?\d+(?:\.\d+)?\b)|(\btrue\b|\bfalse\b|\bnull\b)|(…)/g,
    sh: /((?:^|\s)#.*$)|("(?:[^"\\]|\\.)*"?|'[^']*'?)|(\$\{?[A-Za-z_][A-Za-z0-9_]*\}?|\$\d)|([A-Za-z_][A-Za-z0-9_-]*)|(…)/g,
  };

  function escapeHtml(text) {
    return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }

  function span(kind, text) {
    return `<span class="tk-${kind}">${escapeHtml(text)}</span>`;
  }

  function classifyWord(language, word, rest) {
    if (language === "cs") {
      if (CS_KEYWORDS.has(word)) return "kw";
      if (/^[A-Z]/.test(word)) return /^\s*\(/.test(rest) ? "fn" : "ty";
      return null;
    }
    if (language === "sql") return SQL_KEYWORDS.has(word.toLowerCase()) ? "kw" : null;
    if (language === "sh") return SH_KEYWORDS.has(word) ? "kw" : null;
    return null;
  }

  function highlightLine(language, line) {
    const pattern = PATTERNS[language];
    if (!pattern) return escapeHtml(line);

    let html = "";
    let last = 0;
    pattern.lastIndex = 0;
    for (let match = pattern.exec(line); match; match = pattern.exec(line)) {
      if (match[0] === "") {
        pattern.lastIndex++;
        continue;
      }
      html += escapeHtml(line.slice(last, match.index));
      html += tokenHtml(language, match, line.slice(pattern.lastIndex));
      last = pattern.lastIndex;
    }
    return html + escapeHtml(line.slice(last));
  }

  function tokenHtml(language, match, rest) {
    const text = match[0];
    if (language === "json") {
      if (match[1]) return span(match[2] ? "key" : "str", match[1]) + escapeHtml(match[2] || "");
      if (match[3]) return span("num", text);
      if (match[4]) return span("kw", text);
      return span("el", text);
    }
    if (language === "sh") {
      if (match[1]) return span("com", text);
      if (match[2]) return span("str", text);
      if (match[3]) return span("var", text);
      if (match[4]) {
        const kind = classifyWord(language, text, rest);
        return kind ? span(kind, text) : escapeHtml(text);
      }
      return span("el", text);
    }
    // cs and sql share the group order: comment, string, (char), number, word, ellipsis.
    const groups = language === "cs"
      ? { comment: 1, string: [2, 3], number: 4, word: 5, ellipsis: 6 }
      : { comment: 1, string: [2], number: 3, word: 4, ellipsis: 5 };
    if (match[groups.comment]) return span("com", text);
    if (groups.string.some((index) => match[index])) return span("str", text);
    if (match[groups.number]) return span("num", text);
    if (match[groups.word]) {
      const kind = classifyWord(language, text, rest);
      return kind ? span(kind, text) : escapeHtml(text);
    }
    return span("el", text);
  }

  // Every line becomes its own block, so a long line wraps under its own indentation instead of overflowing.
  function highlightBlock(code) {
    const language = (code.className.match(/lang-(\w+)/) || [])[1] || "text";
    const lines = code.textContent.replace(/\n$/, "").split("\n");
    code.innerHTML = lines.map((line) => {
      const indent = line.length - line.trimStart().length;
      const body = line.trimStart();
      return `<span class="ln" style="--indent:${indent}">${body ? highlightLine(language, body) : ""}</span>`;
    }).join("");
  }

  document.querySelectorAll("pre > code").forEach(highlightBlock);

  // The heading's claim, optionally followed by its second line after a dash.
  function headingText(heading, withSecondLine) {
    return Array.from(heading.childNodes)
      .map((node) => {
        const isSecondLine = node.nodeType === Node.ELEMENT_NODE && node.classList.contains("grad");
        if (!isSecondLine) return node.textContent;
        return withSecondLine ? ` — ${node.textContent}` : "";
      })
      .join("")
      .replace(/\s+/g, " ")
      .trim();
  }

  // ---------- Footer: section bar and slide number ----------

  const firstOfSection = [];
  slides.forEach((slide, index) => {
    const section = Number(slide.dataset.section);
    if (firstOfSection[section - 1] === undefined) firstOfSection[section - 1] = index;
  });

  slides.forEach((slide, index) => {
    const section = Number(slide.dataset.section);
    const heading = slide.querySelector("h1, h2");
    slide.id = `slide-${index + 1}`;
    slide.setAttribute("aria-roledescription", "слайд");
    slide.setAttribute("aria-label", `${index + 1} з ${slides.length}`);
    slide.dataset.heading = heading ? headingText(heading, true) : "";
    slide.dataset.claim = heading ? headingText(heading, false) : "";

    const foot = document.createElement("footer");
    foot.className = "slide-foot";
    const items = sectionNames.map((name, sectionIndex) => {
      const number = String(sectionIndex + 1).padStart(2, "0");
      const isCurrent = sectionIndex + 1 === section;
      return `<li><a href="#/${firstOfSection[sectionIndex] + 1}"` +
        `${isCurrent ? ' class="is-current" aria-current="true"' : ""}>` +
        `<span class="n">${number}</span>${escapeHtml(name)}</a></li>`;
    }).join("");
    foot.innerHTML =
      `<nav aria-label="Розділи"><ol class="sbar">${items}</ol></nav>` +
      `<span class="slide-no">${String(index + 1).padStart(2, "0")} / ${String(slides.length).padStart(2, "0")}</span>`;
    slide.append(foot);
  });

  // ---------- Screenshots that are not there yet ----------

  document.querySelectorAll(".shot img").forEach((image) => {
    const figure = image.closest(".shot");
    const markMissing = () => figure.classList.add("is-missing");
    image.addEventListener("error", markMissing);
    image.addEventListener("load", () => figure.classList.remove("is-missing"));
    if (image.complete && image.naturalWidth === 0) markMissing();
  });

  // ---------- Navigation ----------

  function indexFromHash() {
    const match = /^#\/(\d+)$/.exec(window.location.hash);
    if (!match) return null;
    return Math.min(Math.max(Number(match[1]), 1), slides.length) - 1;
  }

  function show(index, shouldScroll) {
    current = index;
    slides.forEach((slide, slideIndex) => {
      const isCurrent = slideIndex === index;
      slide.classList.toggle("is-current", isCurrent);
      const isHidden = !isFlow && !isCurrent;
      slide.inert = isHidden;
      if (isHidden) slide.setAttribute("aria-hidden", "true");
      else slide.removeAttribute("aria-hidden");
    });
    prevButton.disabled = index === 0;
    nextButton.disabled = index === slides.length - 1;
    status.textContent = `Слайд ${index + 1} з ${slides.length}: ${slides[index].dataset.heading}`;
    if (isFlow && shouldScroll) slides[index].scrollIntoView({ block: "start" });
  }

  function go(index) {
    const target = Math.min(Math.max(index, 0), slides.length - 1);
    if (target === current && indexFromHash() === target) return;
    window.location.hash = `#/${target + 1}`;
  }

  window.addEventListener("hashchange", () => {
    const index = indexFromHash();
    if (index === null) return;
    if (!overview.hidden) closeOverview(false);
    show(index, true);
  });

  prevButton.addEventListener("click", () => go(current - 1));
  nextButton.addEventListener("click", () => go(current + 1));
  overviewButton.addEventListener("click", () => toggleOverview());

  document.addEventListener("keydown", (event) => {
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    if (event.target.closest && event.target.closest("input, textarea, select, [contenteditable]")) return;

    if (event.key === "o" || event.key === "O" || event.key === "о" || event.key === "О") {
      event.preventDefault();
      toggleOverview();
      return;
    }
    if (event.key === "Escape" && !overview.hidden) {
      event.preventDefault();
      closeOverview(true);
      return;
    }
    if (isFlow || !overview.hidden) return;

    const forward = ["ArrowRight", "ArrowDown", "PageDown"].includes(event.key) || (event.key === " " && !event.shiftKey);
    const backward = ["ArrowLeft", "ArrowUp", "PageUp"].includes(event.key) || (event.key === " " && event.shiftKey);
    if (forward) go(current + 1);
    else if (backward) go(current - 1);
    else if (event.key === "Home") go(0);
    else if (event.key === "End") go(slides.length - 1);
    else return;
    event.preventDefault();
  });

  // A horizontal swipe on a touch screen turns the slide.
  let swipeStart = null;
  stage.addEventListener("pointerdown", (event) => {
    swipeStart = event.pointerType === "touch" ? { x: event.clientX, y: event.clientY } : null;
  });
  stage.addEventListener("pointerup", (event) => {
    if (!swipeStart || isFlow) return;
    const dx = event.clientX - swipeStart.x;
    const dy = event.clientY - swipeStart.y;
    swipeStart = null;
    if (Math.abs(dx) > 60 && Math.abs(dy) < Math.abs(dx) / 2) go(current + (dx < 0 ? 1 : -1));
  });

  // ---------- Overview ----------

  let overviewBuilt = false;

  function buildOverview() {
    const grid = overview.querySelector(".overview__grid");
    slides.forEach((slide, index) => {
      const thumb = document.createElement("a");
      thumb.className = "thumb";
      thumb.href = `#/${index + 1}`;

      const frame = document.createElement("span");
      frame.className = "thumb__frame";
      frame.setAttribute("aria-hidden", "true");
      const copy = slide.cloneNode(true);
      copy.removeAttribute("id");
      copy.removeAttribute("aria-hidden");
      copy.classList.add("is-current");
      copy.querySelectorAll("[id]").forEach((node) => node.removeAttribute("id"));
      copy.querySelectorAll("a").forEach((link) => {
        const text = document.createElement("span");
        text.className = link.className;
        text.innerHTML = link.innerHTML;
        link.replaceWith(text);
      });
      copy.inert = true;
      frame.append(copy);

      const caption = document.createElement("span");
      caption.className = "thumb__cap";
      caption.textContent = `${index + 1}. ${slide.dataset.claim}`;

      thumb.append(frame, caption);
      grid.append(thumb);
    });
    overviewBuilt = true;
  }

  function openOverview() {
    if (!overviewBuilt) buildOverview();
    overview.querySelectorAll(".thumb").forEach((thumb, index) => {
      thumb.classList.toggle("is-current", index === current);
      if (index === current) thumb.setAttribute("aria-current", "page");
      else thumb.removeAttribute("aria-current");
    });
    overview.hidden = false;
    stage.inert = true;
    const active = overview.querySelectorAll(".thumb")[current];
    active.focus();
    active.scrollIntoView({ block: "center" });
  }

  function closeOverview(shouldRestoreFocus) {
    overview.hidden = true;
    stage.inert = false;
    if (shouldRestoreFocus) overviewButton.focus();
  }

  function toggleOverview() {
    if (overview.hidden) openOverview();
    else closeOverview(true);
  }

  overview.querySelector("[data-close]").addEventListener("click", () => closeOverview(true));

  // ---------- Fit the stage, or let the slides flow ----------

  function fit() {
    const shouldFlow = flowMedia.matches;
    if (shouldFlow !== isFlow) {
      isFlow = shouldFlow;
      document.body.classList.toggle("flow", isFlow);
      if (current >= 0) show(current, false);
    }
    if (!isFlow) {
      const scale = Math.min(window.innerWidth / STAGE_WIDTH, window.innerHeight / STAGE_HEIGHT);
      stage.style.setProperty("--scale", String(scale));
    }
  }

  window.addEventListener("resize", fit);

  // Printing always uses the full stage, one slide per page, whatever the window.
  window.addEventListener("beforeprint", () => {
    document.body.classList.remove("flow");
    slides.forEach((slide) => { slide.inert = false; });
  });
  window.addEventListener("afterprint", () => {
    document.body.classList.toggle("flow", isFlow);
    show(current, false);
  });

  fit();
  const initial = indexFromHash();
  if (initial === null) window.history.replaceState(null, "", "#/1");
  show(initial === null ? 0 : initial, isFlow && initial !== null);
})();
