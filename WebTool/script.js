const manifest = {
  entries: [],
};

const translations = {
  en: {
    documentTitle: "RemotePhotoSystem Gallery Manager",
    appTitle: "Gallery Manager",
    language: "Language",
    importJson: "Import JSON",
    importTxt: "Import URL TXT",
    exportUnityJson: "Export Unity JSON",
    total: "Total",
    landscape: "Landscape",
    portrait: "Portrait",
    invalidUrl: "Invalid URL",
    selected: "Selected",
    metadataOk: "Metadata OK",
    bulkUrlImport: "Bulk URL Import",
    bulkUrlPlaceholder: "Paste one image URL per line",
    defaultOrientation: "Default Orientation",
    addBlankEntry: "Add Blank Entry",
    importPastedUrls: "Import Pasted URLs",
    search: "Search",
    searchPlaceholder: "URL, ID, tag, note",
    orientation: "Orientation",
    all: "All",
    metadata: "Metadata",
    missing: "Missing",
    ok: "OK",
    error: "Error",
    pageSize: "Page Size",
    selectPage: "Select Page",
    selectFiltered: "Select Filtered",
    clearSelection: "Clear Selection",
    previous: "Previous",
    next: "Next",
    setSelectedLandscape: "Set Selected Landscape",
    setSelectedPortrait: "Set Selected Portrait",
    generateIds: "Generate IDs",
    removeDuplicateUrls: "Remove Duplicate URLs",
    deleteSelected: "Delete Selected",
    tagPlaceholder: "tag",
    addTagToSelected: "Add Tag To Selected",
    removeTagFromSelected: "Remove Tag From Selected",
    probeScope: "Probe Scope",
    filtered: "Filtered",
    missingMetadata: "Missing Metadata",
    forceRefresh: "Force Refresh",
    applyDetectedOrientation: "Apply Detected Orientation",
    probeImageSizes: "Probe Image Sizes",
    idle: "Idle",
    tags: "Tags",
    note: "Note",
    delete: "Delete",
    noEntriesToProbe: "No entries to probe.",
    pageLabel: (page, total) => `Page ${page} / ${total}`,
    probed: (done, total) => `Probed ${done} / ${total}`,
    doneProbed: (total) => `Done. Probed ${total} entries.`,
    statusOk: "OK",
    statusCached: "Cached",
    statusManual: "Manual",
    statusError: "Error",
  },
  ja: {
    documentTitle: "RemotePhotoSystem ギャラリーマネージャー",
    appTitle: "ギャラリーマネージャー",
    language: "言語",
    importJson: "JSON を読み込み",
    importTxt: "URL TXT を読み込み",
    exportUnityJson: "Unity JSON を書き出し",
    total: "合計",
    landscape: "横向き",
    portrait: "縦向き",
    invalidUrl: "無効な URL",
    selected: "選択中",
    metadataOk: "メタデータ OK",
    bulkUrlImport: "URL 一括読み込み",
    bulkUrlPlaceholder: "画像 URL を 1 行に 1 つ貼り付け",
    defaultOrientation: "デフォルト方向",
    addBlankEntry: "空の項目を追加",
    importPastedUrls: "貼り付けた URL を読み込み",
    search: "検索",
    searchPlaceholder: "URL、ID、タグ、メモ",
    orientation: "方向",
    all: "すべて",
    metadata: "メタデータ",
    missing: "未取得",
    ok: "OK",
    error: "エラー",
    pageSize: "ページサイズ",
    selectPage: "ページを選択",
    selectFiltered: "絞り込み結果を選択",
    clearSelection: "選択を解除",
    previous: "前へ",
    next: "次へ",
    setSelectedLandscape: "選択項目を横向きに設定",
    setSelectedPortrait: "選択項目を縦向きに設定",
    generateIds: "ID を生成",
    removeDuplicateUrls: "重複 URL を削除",
    deleteSelected: "選択項目を削除",
    tagPlaceholder: "タグ",
    addTagToSelected: "選択項目にタグを追加",
    removeTagFromSelected: "選択項目からタグを削除",
    probeScope: "取得範囲",
    filtered: "絞り込み結果",
    missingMetadata: "メタデータ未取得",
    forceRefresh: "強制更新",
    applyDetectedOrientation: "検出した方向を適用",
    probeImageSizes: "画像サイズを取得",
    idle: "待機中",
    tags: "タグ",
    note: "メモ",
    delete: "削除",
    noEntriesToProbe: "取得対象の項目がありません。",
    pageLabel: (page, total) => `ページ ${page} / ${total}`,
    probed: (done, total) => `${done} / ${total} 件取得済み`,
    doneProbed: (total) => `完了。${total} 件取得しました。`,
    statusOk: "OK",
    statusCached: "キャッシュ済み",
    statusManual: "手動",
    statusError: "エラー",
  },
  zh: {
    documentTitle: "RemotePhotoSystem 图库管理器",
    appTitle: "图库管理器",
    language: "语言",
    importJson: "导入 JSON",
    importTxt: "导入 URL TXT",
    exportUnityJson: "导出 Unity JSON",
    total: "总数",
    landscape: "横向构图",
    portrait: "纵向构图",
    invalidUrl: "无效 URL",
    selected: "已选择",
    metadataOk: "元数据 OK",
    bulkUrlImport: "批量导入 URL",
    bulkUrlPlaceholder: "每行粘贴一个图片 URL",
    defaultOrientation: "默认方向",
    addBlankEntry: "添加空条目",
    importPastedUrls: "导入粘贴的 URL",
    search: "搜索",
    searchPlaceholder: "URL、ID、标签、备注",
    orientation: "方向",
    all: "全部",
    metadata: "元数据",
    missing: "缺失",
    ok: "OK",
    error: "错误",
    pageSize: "每页数量",
    selectPage: "选择当前页",
    selectFiltered: "选择筛选结果",
    clearSelection: "清除选择",
    previous: "上一页",
    next: "下一页",
    setSelectedLandscape: "将选中项设为横向",
    setSelectedPortrait: "将选中项设为纵向",
    generateIds: "生成 ID",
    removeDuplicateUrls: "移除重复 URL",
    deleteSelected: "删除选中项",
    tagPlaceholder: "标签",
    addTagToSelected: "给选中项添加标签",
    removeTagFromSelected: "从选中项移除标签",
    probeScope: "探测范围",
    filtered: "筛选结果",
    missingMetadata: "缺失元数据",
    forceRefresh: "强制刷新",
    applyDetectedOrientation: "应用检测到的方向",
    probeImageSizes: "探测图片尺寸",
    idle: "空闲",
    tags: "标签",
    note: "备注",
    delete: "删除",
    noEntriesToProbe: "没有需要探测的条目。",
    pageLabel: (page, total) => `第 ${page} / ${total} 页`,
    probed: (done, total) => `已探测 ${done} / ${total}`,
    doneProbed: (total) => `完成。已探测 ${total} 个条目。`,
    statusOk: "OK",
    statusCached: "已缓存",
    statusManual: "手动",
    statusError: "错误",
  },
  ko: {
    documentTitle: "RemotePhotoSystem 갤러리 매니저",
    appTitle: "갤러리 매니저",
    language: "언어",
    importJson: "JSON 가져오기",
    importTxt: "URL TXT 가져오기",
    exportUnityJson: "Unity JSON 내보내기",
    total: "전체",
    landscape: "가로",
    portrait: "세로",
    invalidUrl: "잘못된 URL",
    selected: "선택됨",
    metadataOk: "메타데이터 OK",
    bulkUrlImport: "URL 일괄 가져오기",
    bulkUrlPlaceholder: "이미지 URL 을 한 줄에 하나씩 붙여넣기",
    defaultOrientation: "기본 방향",
    addBlankEntry: "빈 항목 추가",
    importPastedUrls: "붙여넣은 URL 가져오기",
    search: "검색",
    searchPlaceholder: "URL, ID, 태그, 메모",
    orientation: "방향",
    all: "전체",
    metadata: "메타데이터",
    missing: "없음",
    ok: "OK",
    error: "오류",
    pageSize: "페이지 크기",
    selectPage: "현재 페이지 선택",
    selectFiltered: "필터 결과 선택",
    clearSelection: "선택 해제",
    previous: "이전",
    next: "다음",
    setSelectedLandscape: "선택 항목을 가로로 설정",
    setSelectedPortrait: "선택 항목을 세로로 설정",
    generateIds: "ID 생성",
    removeDuplicateUrls: "중복 URL 제거",
    deleteSelected: "선택 항목 삭제",
    tagPlaceholder: "태그",
    addTagToSelected: "선택 항목에 태그 추가",
    removeTagFromSelected: "선택 항목에서 태그 제거",
    probeScope: "탐색 범위",
    filtered: "필터 결과",
    missingMetadata: "메타데이터 없음",
    forceRefresh: "강제 새로고침",
    applyDetectedOrientation: "감지된 방향 적용",
    probeImageSizes: "이미지 크기 탐색",
    idle: "대기 중",
    tags: "태그",
    note: "메모",
    delete: "삭제",
    noEntriesToProbe: "탐색할 항목이 없습니다.",
    pageLabel: (page, total) => `페이지 ${page} / ${total}`,
    probed: (done, total) => `${done} / ${total} 탐색 완료`,
    doneProbed: (total) => `완료. ${total}개 항목을 탐색했습니다.`,
    statusOk: "OK",
    statusCached: "캐시됨",
    statusManual: "수동",
    statusError: "오류",
  },
};

const state = {
  selected: new Set(),
  filteredIndices: [],
  page: 0,
  pageSize: 100,
  probing: false,
  language: getInitialLanguage(),
};

const metadataDbName = "RemotePhotoSystemGalleryMetadata";
const metadataStoreName = "metadata";

const elements = {
  tableBody: document.getElementById("entryTableBody"),
  jsonInput: document.getElementById("jsonInput"),
  txtInput: document.getElementById("txtInput"),
  languageSelect: document.getElementById("languageSelect"),
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
elements.languageSelect.addEventListener("change", () => setLanguage(elements.languageSelect.value));

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
          <option value="Landscape" ${entry.orientation === "Landscape" ? "selected" : ""}>${escapeHtml(t("landscape"))}</option>
          <option value="Portrait" ${entry.orientation === "Portrait" ? "selected" : ""}>${escapeHtml(t("portrait"))}</option>
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
        <button data-remove="${entryIndex}">${escapeHtml(t("delete"))}</button>
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
  document.getElementById("pageLabel").textContent = t("pageLabel", Math.min(state.page + 1, pageCount), pageCount);
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
    elements.probeStatus.textContent = t("noEntriesToProbe");
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
    elements.probeStatus.textContent = t("probed", completed, indices.length);
  });

  state.probing = false;
  elements.probeStatus.textContent = t("doneProbed", indices.length);
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
    return t("missing");
  }

  if (metadata.width > 0 && metadata.height > 0) {
    return `${formatMetadataStatus(metadata.status)}: ${metadata.width} x ${metadata.height}`;
  }

  return `${formatMetadataStatus(metadata.status)}${metadata.error ? `: ${metadata.error}` : ""}`;
}

function formatMetadataStatus(status) {
  if (status === "ok") {
    return t("statusOk");
  }

  if (status === "cached") {
    return t("statusCached");
  }

  if (status === "manual") {
    return t("statusManual");
  }

  if (status === "error") {
    return t("statusError");
  }

  return status;
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

function getInitialLanguage() {
  const stored = localStorage.getItem("RemotePhotoSystemGalleryLanguage");
  if (translations[stored]) {
    return stored;
  }

  const browserLanguage = navigator.language.slice(0, 2).toLowerCase();
  return translations[browserLanguage] ? browserLanguage : "en";
}

function setLanguage(language) {
  state.language = translations[language] ? language : "en";
  localStorage.setItem("RemotePhotoSystemGalleryLanguage", state.language);
  applyTranslations();
  render();
}

function applyTranslations() {
  document.documentElement.lang = state.language;
  document.title = t("documentTitle");
  elements.languageSelect.value = state.language;

  document.querySelectorAll("[data-i18n]").forEach((element) => {
    element.textContent = t(element.dataset.i18n);
  });

  document.querySelectorAll("[data-i18n-placeholder]").forEach((element) => {
    element.placeholder = t(element.dataset.i18nPlaceholder);
  });
}

function t(key, ...args) {
  const dictionary = translations[state.language] || translations.en;
  const value = dictionary[key] ?? translations.en[key] ?? key;
  return typeof value === "function" ? value(...args) : value;
}

applyTranslations();
render();
