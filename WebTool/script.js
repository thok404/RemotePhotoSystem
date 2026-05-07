const manifest = {
  entries: [],
};

const state = {
  selected: new Set(),
  filteredIndices: [],
  page: 0,
  pageSize: 100,
  probing: false,
};

const metadataDbName = "RemotePhotoSystemGalleryMetadata";
const metadataStoreName = "metadata";

const elements = {
  tableBody: document.getElementById("entryTableBody"),
  jsonInput: document.getElementById("jsonInput"),
  txtInput: document.getElementById("txtInput"),
  bulkUrls: document.getElementById("bulkUrls"),
  bulkOrientation: document.getElementById("bulkOrientation"),
  searchInput: document.getElementById("searchInput"),
  orientationFilter: document.getElementById("orientationFilter"),
  metadataFilter: document.getElementById("metadataFilter"),
  pageSizeSelect: document.getElementById("pageSizeSelect"),
  tagInput: document.getElementById("tagInput"),
  probeScope: document.getElementById("probeScope"),
  forceRefreshProbe: document.getElementById("forceRefreshProbe"),
  overwriteOrientation: document.getElementById("overwriteOrientation"),
  probeProgress: document.getElementById("probeProgress"),
  probeStatus: document.getElementById("probeStatus"),
};

document.getElementById("importJsonButton").addEventListener("click", () => elements.jsonInput.click());
document.getElementById("importTxtButton").addEventListener("click", () => elements.txtInput.click());
document.getElementById("exportButton").addEventListener("click", exportManifest);
document.getElementById("addEntryButton").addEventListener("click", addBlankEntry);
document.getElementById("bulkImportButton").addEventListener("click", importPastedUrls);
document.getElementById("selectPageButton").addEventListener("click", selectPage);
document.getElementById("selectFilteredButton").addEventListener("click", selectFiltered);
document.getElementById("clearSelectionButton").addEventListener("click", clearSelection);
document.getElementById("prevPageButton").addEventListener("click", () => movePage(-1));
document.getElementById("nextPageButton").addEventListener("click", () => movePage(1));
document.getElementById("batchLandscapeButton").addEventListener("click", () => setSelectedOrientation("Landscape"));
document.getElementById("batchPortraitButton").addEventListener("click", () => setSelectedOrientation("Portrait"));
document.getElementById("generateIdsButton").addEventListener("click", generateIdsForSelected);
document.getElementById("dedupeButton").addEventListener("click", removeDuplicateUrls);
document.getElementById("deleteSelectedButton").addEventListener("click", deleteSelected);
document.getElementById("addTagButton").addEventListener("click", () => editSelectedTag(true));
document.getElementById("removeTagButton").addEventListener("click", () => editSelectedTag(false));
document.getElementById("probeButton").addEventListener("click", probeImageSizes);

elements.searchInput.addEventListener("input", () => {
  state.page = 0;
  render();
});
elements.orientationFilter.addEventListener("change", () => {
  state.page = 0;
  render();
});
elements.metadataFilter.addEventListener("change", () => {
  state.page = 0;
  render();
});
elements.pageSizeSelect.addEventListener("change", () => {
  state.pageSize = Number(elements.pageSizeSelect.value);
  state.page = 0;
  render();
});

elements.jsonInput.addEventListener("change", async (event) => {
  const file = event.target.files[0];
  if (!file) {
    return;
  }

  const text = await file.text();
  importJsonText(text);
  event.target.value = "";
});

elements.txtInput.addEventListener("change", async (event) => {
  const file = event.target.files[0];
  if (!file) {
    return;
  }

  const text = await file.text();
  addUrls(extractUrls(text), "Landscape");
  event.target.value = "";
});

elements.tableBody.addEventListener("input", onTableInput);
elements.tableBody.addEventListener("change", onTableInput);
elements.tableBody.addEventListener("click", onTableClick);

function addBlankEntry() {
  manifest.entries.push(createEntry("", elements.bulkOrientation.value));
  state.page = Math.max(0, Math.ceil(manifest.entries.length / state.pageSize) - 1);
  render();
}

function importPastedUrls() {
  const urls = extractUrls(elements.bulkUrls.value);
  addUrls(urls, elements.bulkOrientation.value);
  elements.bulkUrls.value = "";
}

function importJsonText(text) {
  const parsed = JSON.parse(text);
  manifest.entries = Array.isArray(parsed.entries) ? parsed.entries.map(normalizeEntry) : [];
  state.selected.clear();
  state.page = 0;
  render();
}

function addUrls(urls, orientation) {
  const existing = new Set(manifest.entries.map((entry) => entry.url));
  urls.forEach((url) => {
    if (!RemotePhotoUrlUtilityIsValid(url) || existing.has(url)) {
      return;
    }

    manifest.entries.push(createEntry(url, orientation));
    existing.add(url);
  });

  render();
}

function createEntry(url, orientation) {
  return {
    id: buildUniqueId(url),
    url,
    orientation: orientation === "Portrait" ? "Portrait" : "Landscape",
    tags: [],
    note: "",
    metadata: null,
  };
}

function normalizeEntry(entry) {
  return {
    id: typeof entry?.id === "string" && entry.id.trim() ? entry.id.trim() : buildUniqueId(entry?.url || ""),
    url: typeof entry?.url === "string" ? entry.url.trim() : "",
    orientation: entry?.orientation === "Portrait" ? "Portrait" : "Landscape",
    tags: Array.isArray(entry?.tags) ? entry.tags.map(String).filter(Boolean) : [],
    note: typeof entry?.note === "string" ? entry.note : "",
    metadata: normalizeMetadata(entry?.metadata),
  };
}

function normalizeMetadata(metadata) {
  if (!metadata || typeof metadata !== "object") {
    return null;
  }

  return {
    width: Number(metadata.width) || 0,
    height: Number(metadata.height) || 0,
    checkedAt: typeof metadata.checkedAt === "string" ? metadata.checkedAt : "",
    status: typeof metadata.status === "string" ? metadata.status : "",
    error: typeof metadata.error === "string" ? metadata.error : "",
  };
}

function extractUrls(text) {
  const matches = String(text).match(/https?:\/\/[^\s"'<>]+/gi);
  return matches ? matches.map((url) => url.trim()) : [];
}

function render() {
  state.filteredIndices = getFilteredIndices();

  const pageCount = getPageCount();
  if (state.page >= pageCount) {
    state.page = Math.max(0, pageCount - 1);
  }

  const start = state.page * state.pageSize;
  const pageIndices = state.filteredIndices.slice(start, start + state.pageSize);

  elements.tableBody.innerHTML = pageIndices.map((entryIndex) => renderRow(entryIndex)).join("");
  updateSummary();
}

function renderRow(entryIndex) {
  const entry = manifest.entries[entryIndex];
  const metadataText = formatMetadata(entry.metadata);

  return `
    <tr>
      <td class="check-cell">
        <input type="checkbox" data-select="${entryIndex}" ${state.selected.has(entryIndex) ? "checked" : ""}>
      </td>
      <td class="id-cell">
        <input type="text" data-field="id" data-index="${entryIndex}" value="${escapeHtml(entry.id)}">
      </td>
      <td class="url-cell">
        <input type="text" data-field="url" data-index="${entryIndex}" value="${escapeHtml(entry.url)}">
      </td>
      <td class="orientation-cell">
        <select data-field="orientation" data-index="${entryIndex}">
          <option value="Landscape" ${entry.orientation === "Landscape" ? "selected" : ""}>Landscape</option>
          <option value="Portrait" ${entry.orientation === "Portrait" ? "selected" : ""}>Portrait</option>
        </select>
      </td>
      <td>
        <input type="text" data-field="tags" data-index="${entryIndex}" value="${escapeHtml(entry.tags.join(", "))}">
      </td>
      <td>
        <input type="text" data-field="note" data-index="${entryIndex}" value="${escapeHtml(entry.note)}">
      </td>
      <td class="metadata-cell">${escapeHtml(metadataText)}</td>
      <td class="row-actions">
        <button data-remove="${entryIndex}">Delete</button>
      </td>
    </tr>
  `;
}

function onTableInput(event) {
  const index = Number(event.target.dataset.index);
  const field = event.target.dataset.field;
  if (!Number.isFinite(index) || !field || !manifest.entries[index]) {
    return;
  }

  if (field === "tags") {
    manifest.entries[index].tags = parseTags(event.target.value);
  } else {
    manifest.entries[index][field] = event.target.value;
  }

  updateSummary();
}

function onTableClick(event) {
  const selectedIndex = event.target.dataset.select;
  if (selectedIndex !== undefined) {
    const index = Number(selectedIndex);
    if (event.target.checked) {
      state.selected.add(index);
    } else {
      state.selected.delete(index);
    }
    updateSummary();
    return;
  }

  const removeIndex = event.target.dataset.remove;
  if (removeIndex !== undefined) {
    manifest.entries.splice(Number(removeIndex), 1);
    state.selected.clear();
    render();
  }
}

function getFilteredIndices() {
  const query = elements.searchInput.value.trim().toLowerCase();
  const orientation = elements.orientationFilter.value;
  const metadataFilter = elements.metadataFilter.value;
  const indices = [];

  for (let index = 0; index < manifest.entries.length; index += 1) {
    const entry = manifest.entries[index];
    if (orientation !== "All" && entry.orientation !== orientation) {
      continue;
    }

    if (!matchesMetadataFilter(entry, metadataFilter)) {
      continue;
    }

    if (query && !entryMatchesQuery(entry, query)) {
      continue;
    }

    indices.push(index);
  }

  return indices;
}

function matchesMetadataFilter(entry, filter) {
  if (filter === "All") {
    return true;
  }

  const status = entry.metadata?.status || "";
  if (filter === "Missing") {
    return !entry.metadata || status === "";
  }

  if (filter === "Ok") {
    return status === "ok" || status === "cached" || status === "manual";
  }

  return status === "error";
}

function entryMatchesQuery(entry, query) {
  return entry.id.toLowerCase().includes(query) ||
    entry.url.toLowerCase().includes(query) ||
    entry.note.toLowerCase().includes(query) ||
    entry.tags.join(" ").toLowerCase().includes(query);
}

function updateSummary() {
  const total = manifest.entries.length;
  const landscape = manifest.entries.filter((entry) => entry.orientation === "Landscape").length;
  const portrait = manifest.entries.filter((entry) => entry.orientation === "Portrait").length;
  const invalid = manifest.entries.filter((entry) => !RemotePhotoUrlUtilityIsValid(entry.url)).length;
  const metadataOk = manifest.entries.filter((entry) => matchesMetadataFilter(entry, "Ok")).length;
  const pageCount = getPageCount();

  document.getElementById("totalCount").textContent = String(total);
  document.getElementById("landscapeCount").textContent = String(landscape);
  document.getElementById("portraitCount").textContent = String(portrait);
  document.getElementById("invalidCount").textContent = String(invalid);
  document.getElementById("selectedCount").textContent = String(state.selected.size);
  document.getElementById("metadataCount").textContent = String(metadataOk);
  document.getElementById("pageLabel").textContent = `Page ${Math.min(state.page + 1, pageCount)} / ${pageCount}`;
}

function getPageCount() {
  return Math.max(1, Math.ceil(state.filteredIndices.length / state.pageSize));
}

function movePage(direction) {
  const nextPage = state.page + direction;
  state.page = Math.max(0, Math.min(getPageCount() - 1, nextPage));
  render();
}

function selectPage() {
  const start = state.page * state.pageSize;
  state.filteredIndices.slice(start, start + state.pageSize).forEach((index) => state.selected.add(index));
  render();
}

function selectFiltered() {
  state.filteredIndices.forEach((index) => state.selected.add(index));
  render();
}

function clearSelection() {
  state.selected.clear();
  render();
}

function setSelectedOrientation(orientation) {
  getSelectedIndices().forEach((index) => {
    manifest.entries[index].orientation = orientation;
  });
  render();
}

function generateIdsForSelected() {
  getSelectedOrAllIndices().forEach((index) => {
    manifest.entries[index].id = buildUniqueId(manifest.entries[index].url, index);
  });
  render();
}

function removeDuplicateUrls() {
  const seen = new Set();
  manifest.entries = manifest.entries.filter((entry) => {
    const key = entry.url.trim();
    if (!key || seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
  state.selected.clear();
  render();
}

function deleteSelected() {
  const selected = state.selected;
  manifest.entries = manifest.entries.filter((entry, index) => !selected.has(index));
  state.selected.clear();
  render();
}

function editSelectedTag(add) {
  const tag = elements.tagInput.value.trim();
  if (!tag) {
    return;
  }

  getSelectedIndices().forEach((index) => {
    const tags = new Set(manifest.entries[index].tags);
    if (add) {
      tags.add(tag);
    } else {
      tags.delete(tag);
    }
    manifest.entries[index].tags = Array.from(tags);
  });

  render();
}

async function probeImageSizes() {
  if (state.probing) {
    return;
  }

  const indices = getProbeIndices();
  if (indices.length === 0) {
    elements.probeStatus.textContent = "No entries to probe.";
    return;
  }

  state.probing = true;
  elements.probeProgress.max = indices.length;
  elements.probeProgress.value = 0;

  const force = elements.forceRefreshProbe.checked;
  const overwriteOrientation = elements.overwriteOrientation.checked;
  let completed = 0;

  await runWithConcurrency(indices, 6, async (index) => {
    const entry = manifest.entries[index];
    const metadata = await getOrProbeMetadata(entry.url, force);
    entry.metadata = metadata;

    if (overwriteOrientation && metadata.width > 0 && metadata.height > 0) {
      entry.orientation = metadata.width >= metadata.height ? "Landscape" : "Portrait";
    }

    completed += 1;
    elements.probeProgress.value = completed;
    elements.probeStatus.textContent = `Probed ${completed} / ${indices.length}`;
  });

  state.probing = false;
  elements.probeStatus.textContent = `Done. Probed ${indices.length} entries.`;
  render();
}

function getProbeIndices() {
  const scope = elements.probeScope.value;
  if (scope === "Selected") {
    return getSelectedIndices();
  }

  if (scope === "Filtered") {
    return state.filteredIndices.slice();
  }

  if (scope === "Missing") {
    return manifest.entries
      .map((entry, index) => ({ entry, index }))
      .filter((item) => !item.entry.metadata || !item.entry.metadata.status)
      .map((item) => item.index);
  }

  return manifest.entries.map((entry, index) => index);
}

async function getOrProbeMetadata(url, force) {
  if (!RemotePhotoUrlUtilityIsValid(url)) {
    return buildMetadata(0, 0, "error", "Invalid URL");
  }

  if (!force) {
    const cached = await readCachedMetadata(url);
    if (cached) {
      return { ...cached, status: cached.status === "ok" ? "cached" : cached.status };
    }
  }

  try {
    const size = await loadImageSize(url);
    const metadata = buildMetadata(size.width, size.height, "ok", "");
    await writeCachedMetadata(url, metadata);
    return metadata;
  } catch (error) {
    const metadata = buildMetadata(0, 0, "error", error?.message || "Image load failed");
    await writeCachedMetadata(url, metadata);
    return metadata;
  }
}

function loadImageSize(url) {
  return new Promise((resolve, reject) => {
    const image = new Image();
    const timeout = window.setTimeout(() => {
      image.src = "";
      reject(new Error("Timed out"));
    }, 20000);

    image.onload = () => {
      window.clearTimeout(timeout);
      resolve({ width: image.naturalWidth, height: image.naturalHeight });
    };
    image.onerror = () => {
      window.clearTimeout(timeout);
      reject(new Error("Image load failed"));
    };
    image.src = url;
  });
}

function buildMetadata(width, height, status, error) {
  return {
    width,
    height,
    checkedAt: new Date().toISOString(),
    status,
    error,
  };
}

function runWithConcurrency(items, limit, worker) {
  let cursor = 0;
  const workers = Array.from({ length: Math.min(limit, items.length) }, async () => {
    while (cursor < items.length) {
      const item = items[cursor];
      cursor += 1;
      await worker(item);
    }
  });

  return Promise.all(workers);
}

function openMetadataDb() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(metadataDbName, 1);
    request.onupgradeneeded = () => {
      request.result.createObjectStore(metadataStoreName, { keyPath: "url" });
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function readCachedMetadata(url) {
  const db = await openMetadataDb();
  return new Promise((resolve) => {
    const tx = db.transaction(metadataStoreName, "readonly");
    const request = tx.objectStore(metadataStoreName).get(url);
    request.onsuccess = () => resolve(request.result?.metadata || null);
    request.onerror = () => resolve(null);
  });
}

async function writeCachedMetadata(url, metadata) {
  const db = await openMetadataDb();
  return new Promise((resolve) => {
    const tx = db.transaction(metadataStoreName, "readwrite");
    tx.objectStore(metadataStoreName).put({ url, metadata });
    tx.oncomplete = () => resolve();
    tx.onerror = () => resolve();
  });
}

function exportManifest() {
  const cleanEntries = manifest.entries.map((entry) => ({
    id: entry.id,
    url: entry.url,
    orientation: entry.orientation,
    tags: entry.tags,
    note: entry.note,
    metadata: entry.metadata,
  }));

  const blob = new Blob([JSON.stringify({ entries: cleanEntries }, null, 2)], { type: "application/json" });
  const link = document.createElement("a");
  link.href = URL.createObjectURL(blob);
  link.download = "RemotePhotoGalleryConfig.json";
  link.click();
  URL.revokeObjectURL(link.href);
}

function getSelectedIndices() {
  return Array.from(state.selected).filter((index) => manifest.entries[index]);
}

function getSelectedOrAllIndices() {
  return state.selected.size > 0 ? getSelectedIndices() : manifest.entries.map((entry, index) => index);
}

function parseTags(value) {
  return String(value)
    .split(",")
    .map((tag) => tag.trim())
    .filter(Boolean);
}

function formatMetadata(metadata) {
  if (!metadata || !metadata.status) {
    return "Missing";
  }

  if (metadata.width > 0 && metadata.height > 0) {
    return `${metadata.status}: ${metadata.width} x ${metadata.height}`;
  }

  return `${metadata.status}${metadata.error ? `: ${metadata.error}` : ""}`;
}

function buildUniqueId(url, fallbackIndex = manifest.entries.length) {
  const base = buildIdBase(url) || `photo_${String(fallbackIndex + 1).padStart(6, "0")}`;
  const existing = new Set(manifest.entries.map((entry) => entry.id));
  let candidate = base;
  let suffix = 2;

  while (existing.has(candidate)) {
    candidate = `${base}_${suffix}`;
    suffix += 1;
  }

  return candidate;
}

function buildIdBase(url) {
  try {
    const parsed = new URL(url);
    const fileName = parsed.pathname.split("/").filter(Boolean).pop() || "";
    const withoutExtension = fileName.replace(/\.[a-z0-9]+$/i, "");
    return sanitizeId(withoutExtension);
  } catch {
    return "";
  }
}

function sanitizeId(value) {
  return String(value)
    .toLowerCase()
    .replace(/[^a-z0-9_-]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .slice(0, 80);
}

function RemotePhotoUrlUtilityIsValid(value) {
  return /^https?:\/\//i.test(String(value || ""));
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll("\"", "&quot;");
}

render();
