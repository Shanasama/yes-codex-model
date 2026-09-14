const ui = {
  list: document.querySelector("#skin-list"),
  search: document.querySelector("#skin-search"),
  importButton: document.querySelector("#import-button"),
  importInput: document.querySelector("#skin-import"),
  gifInput: document.querySelector("#gif-input"),
  replaceButton: document.querySelector("#replace-button"),
  title: document.querySelector("#skin-title"),
  preview: document.querySelector("#main-preview"),
  previewKicker: document.querySelector("#preview-kicker"),
  previewTitle: document.querySelector("#preview-title"),
  previewMeta: document.querySelector("#preview-meta"),
  dropZone: document.querySelector("#drop-zone"),
  stateGrid: document.querySelector("#state-grid"),
  stateHealth: document.querySelector("#state-health"),
  duplicate: document.querySelector("#duplicate-button"),
  export: document.querySelector("#export-button"),
  apply: document.querySelector("#apply-button"),
  builtin: document.querySelector("#builtin-badge"),
  metaName: document.querySelector("#meta-name"),
  metaAuthor: document.querySelector("#meta-author"),
  metaDescription: document.querySelector("#meta-description"),
  saveMeta: document.querySelector("#save-meta"),
  runtimeSection: document.querySelector("#animation-mode-section"),
  runtimeIndicator: document.querySelector("#runtime-indicator"),
  runtimeTitle: document.querySelector("#runtime-title"),
  runtimeDetail: document.querySelector("#runtime-detail"),
  runtimeTechnicalDetail: document.querySelector("#runtime-technical-detail"),
  runtimePath: document.querySelector("#runtime-path"),
  runtimeSave: document.querySelector("#runtime-save"),
  runtimePatch: document.querySelector("#runtime-patch"),
  runtimeRestore: document.querySelector("#runtime-restore"),
  lowPerformance: document.querySelector("#low-performance"),
  usageIndicator: document.querySelector("#usage-indicator"),
  usageEnabled: document.querySelector("#usage-enabled"),
  usageApiPreset: document.querySelector("#usage-api-preset"),
  usageApiBase: document.querySelector("#usage-api-base"),
  usageKey: document.querySelector("#usage-key"),
  usageKeyHint: document.querySelector("#usage-key-hint"),
  usageClearKey: document.querySelector("#usage-clear-key"),
  usageSpeed: document.querySelector("#usage-speed"),
  usageTint: document.querySelector("#usage-tint"),
  usageSensitivity: document.querySelector("#usage-sensitivity"),
  usageSave: document.querySelector("#usage-save"),
  usageLive: document.querySelector("#usage-live"),
  usageTitle: document.querySelector("#usage-title"),
  usageDetail: document.querySelector("#usage-detail"),
  usageTokens: document.querySelector("#usage-tokens"),
  usageSpeedValue: document.querySelector("#usage-speed-value"),
  usageRedValue: document.querySelector("#usage-red-value"),
  busy: document.querySelector("#busy"),
  toast: document.querySelector("#toast")
};

// DeepSeek 当前不提供本页面所需的 Organization Usage 接口，暂时关闭联动。
const USAGE_LINK_ENABLED = false;

let model = null;
let selectedId = null;
let selectedState = "idle";
let toastTimer = null;
let usageRefreshTimer = null;
let lowPerformanceMode = window.localStorage.getItem("codex-skin-low-performance") === "true";

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, (character) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;"
  }[character]));
}

function formatBytes(bytes) {
  if (!Number.isFinite(bytes)) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function formatDuration(ms) {
  if (ms < 1000) return `${ms} ms`;
  return `${(ms / 1000).toFixed(1)} 秒`;
}

function assetUrl(skin, relative, hash = "") {
  const path = relative.split("/").map(encodeURIComponent).join("/");
  return `/api/asset/${encodeURIComponent(skin.id)}/${path}?v=${encodeURIComponent(hash)}`;
}

function freezeThumbnail(image) {
  if (!lowPerformanceMode || image.dataset.frozen === "true") return;
  const capture = () => {
    if (!lowPerformanceMode || !image.naturalWidth || !image.naturalHeight) return;
    const canvas = document.createElement("canvas");
    canvas.width = image.naturalWidth;
    canvas.height = image.naturalHeight;
    canvas.getContext("2d").drawImage(image, 0, 0);
    image.src = canvas.toDataURL("image/png");
    image.dataset.frozen = "true";
  };
  if (image.complete) capture();
  else image.addEventListener("load", capture, { once: true });
}

function freezeThumbnails() {
  if (lowPerformanceMode) document.querySelectorAll(".skin-thumb-image, .state-thumb-image").forEach(freezeThumbnail);
}

function showToast(message, error = false) {
  clearTimeout(toastTimer);
  ui.toast.textContent = message;
  ui.toast.className = `toast show${error ? " error" : ""}`;
  toastTimer = setTimeout(() => { ui.toast.className = "toast"; }, 3600);
}

function setBusy(value, label = "正在处理") {
  ui.busy.hidden = !value;
  ui.busy.querySelector("strong").textContent = label;
}

async function api(url, options = {}) {
  const response = await fetch(url, options);
  const payload = await response.json().catch(() => ({ ok: false, error: `HTTP ${response.status}` }));
  if (!response.ok || !payload.ok) throw new Error(payload.error || `请求失败：${response.status}`);
  return payload.data;
}

function selectedSkin() {
  return model?.skins.find((skin) => skin.id === selectedId) || null;
}

function renderLibrary() {
  const query = ui.search.value.trim().toLowerCase();
  const skins = model.skins.filter((skin) => `${skin.name} ${skin.author?.name || ""}`.toLowerCase().includes(query));
  ui.list.innerHTML = skins.map((skin) => {
    // Keep the library thumbnail tied to the editable idle animation. The fallback
    // atlas is the bundled compatibility image and does not change when a GIF is replaced.
    const idle = skin.states?.idle;
    const thumbnail = skin.thumbnail === idle?.file
      ? idle
      : (skin.thumbnail ? { file: skin.thumbnail } : idle);
    const thumb = thumbnail
      ? `<span class="skin-thumb"><img class="skin-thumb-image" src="${assetUrl(skin, thumbnail.file, thumbnail.sha256 || "")}" alt=""></span>`
      : `<span class="skin-thumb"><img class="sprite-sheet row-0" src="${assetUrl(skin, skin.fallback.file)}" alt=""></span>`;
    return `
      <button class="skin-item ${skin.id === selectedId ? "active" : ""}" type="button" data-skin="${escapeHtml(skin.id)}">
        ${thumb}
        <span>
          <strong>${escapeHtml(skin.name)}</strong>
          <span>${escapeHtml(skin.author?.name || "Unknown")} · ${formatBytes(skin.bytes)}</span>
        </span>
        <b class="skin-origin">${skin.builtIn ? "自带皮肤" : "我的皮肤"}</b>
      </button>`;
  }).join("");
  freezeThumbnails();
}

function renderRuntime(runtime) {
  const states = {
    "gif-patched": {
      badge: "已开启",
      indicatorClass: "ready",
      sectionClass: "active",
      title: "完整动画正在使用",
      detail: "工作、待机等动画会完整循环，不再中途切回默认动作。"
    },
    baseline: {
      badge: "还差一步",
      indicatorClass: "attention",
      sectionClass: "attention",
      title: "完整动画尚未开启",
      detail: "点击下方按钮开启，完成后按提示重新启动 Codex。"
    },
    "gif-patched-legacy": {
      badge: "可升级",
      indicatorClass: "attention",
      sectionClass: "attention",
      title: "可以加入工作强度联动",
      detail: "更新后可让工作动画随 API 用量加速并变红，原有 GIF 不会改变。"
    },
    unsupported: {
      badge: "暂不支持",
      indicatorClass: "bad",
      sectionClass: "error",
      title: "当前版本暂时无法开启",
      detail: "为保护 Codex，本次不会改动程序文件；皮肤仍可使用兼容动画。"
    },
    "not-found": {
      badge: "还差一步",
      indicatorClass: "attention",
      sectionClass: "attention",
      title: "没有找到 Codex 安装位置",
      detail: "先在高级设置中重新检查；仍找不到时，再填写程序文件位置。"
    },
    unknown: {
      badge: "暂不支持",
      indicatorClass: "bad",
      sectionClass: "error",
      title: "无法确认当前状态",
      detail: "为保护 Codex，软件已暂停改动。可以在高级设置中重新检查。"
    }
  };
  const view = states[runtime.state] || states.unknown;
  ui.runtimeSection.className = `inspector-section runtime-section ${view.sectionClass}`;
  ui.runtimeIndicator.textContent = view.badge;
  ui.runtimeIndicator.className = `runtime-indicator ${view.indicatorClass}`;
  ui.runtimeTitle.textContent = view.title;
  ui.runtimeDetail.textContent = view.detail;
  ui.runtimeTechnicalDetail.textContent = runtime.asar
    ? `已定位：${runtime.asar}`
    : (runtime.message || "尚未自动找到程序文件位置");
  if (runtime.asar) ui.runtimePath.value = runtime.asar;
  const canPatch = runtime.state === "baseline" || runtime.state === "gif-patched-legacy";
  ui.runtimePatch.textContent = runtime.state === "gif-patched-legacy" ? "更新动画功能" : "一键开启完整动画";
  ui.runtimePatch.hidden = !canPatch;
  ui.runtimePatch.disabled = !canPatch;
  ui.runtimeRestore.disabled = runtime.state !== "gif-patched" && runtime.state !== "gif-patched-legacy";
}

function applyUsagePreview() {
  const usage = model?.usage;
  const active = selectedState === "running" && usage?.state?.enabled && usage.state.status === "active";
  const level = active ? Math.max(0, Math.min(4, Math.ceil((usage.state.intensity || 0) * 4))) : 0;
  ui.preview.dataset.usageLevel = String(level);
}

function renderUsage(usage, { syncControls = false } = {}) {
  if (!USAGE_LINK_ENABLED) return;
  const settings = usage?.settings || {};
  const state = usage?.state || { status: "disabled", speedMultiplier: 1, redPercent: 0, tokensLast30s: 0 };
  const views = {
    disabled: { badge: "未开启", className: "", title: "联动未开启" },
    "needs-key": { badge: "需要 Key", className: "attention", title: "请先保存 API Key" },
    waiting: { badge: "已连接", className: "ready", title: "等待下一次统计" },
    idle: { badge: "已连接", className: "ready", title: "当前用量平稳" },
    active: { badge: "正在联动", className: "hot", title: "工作动画已随用量变化" },
    error: { badge: "连接失败", className: "bad", title: "暂时无法读取用量" }
  };
  const view = views[state.status] || views.error;

  if (syncControls) {
    ui.usageEnabled.checked = settings.enabled === true;
    ui.usageSpeed.checked = settings.speedEnabled !== false;
    ui.usageTint.checked = settings.tintEnabled !== false;
    ui.usageSensitivity.value = settings.sensitivity || "balanced";
    ui.usageApiBase.value = settings.apiBaseUrl || "https://api.deepseek.com";
    ui.usageApiPreset.value = ["https://api.deepseek.com", "https://api.openai.com"].includes(ui.usageApiBase.value)
      ? ui.usageApiBase.value
      : "custom";
  }
  ui.usageKey.placeholder = settings.keyConfigured ? "已保存，留空表示不更换" : "sk-...";
  ui.usageKeyHint.textContent = settings.keyConfigured
    ? `Key 已保存在本机${settings.keyHint ? `，末尾 ${settings.keyHint}` : ""}`
    : "填写此服务用于读取用量的 API Key。";
  ui.usageClearKey.hidden = !settings.keyConfigured;
  ui.usageIndicator.textContent = view.badge;
  ui.usageIndicator.className = `usage-indicator ${view.className}`.trim();
  ui.usageLive.hidden = !settings.enabled;
  ui.usageTitle.textContent = view.title;
  ui.usageDetail.textContent = state.message || "每 30 秒自动刷新一次";
  ui.usageTokens.textContent = `${Math.max(0, Math.round(state.tokensLast30s || 0)).toLocaleString("zh-CN")} tokens`;
  ui.usageSpeedValue.textContent = `${Number(state.speedMultiplier || 1).toFixed(2)}×`;
  ui.usageRedValue.textContent = `${Math.max(0, Math.round(state.redPercent || 0))}%`;
  applyUsagePreview();
}

function renderSelected() {
  const skin = selectedSkin();
  if (!skin) return;
  const state = skin.states[selectedState] || skin.states.idle;
  selectedState = state.id;
  ui.title.textContent = skin.name;
  ui.previewKicker.textContent = state.label;
  ui.previewTitle.textContent = state.hint;
  ui.previewMeta.textContent = `${state.frames} 帧 · ${state.width}×${state.height} · ${formatDuration(state.durationMs)} · ${formatBytes(state.bytes)}`;
  ui.preview.src = assetUrl(skin, state.file, state.sha256);
  ui.preview.alt = `${skin.name} ${state.label}动画`;
  ui.stateHealth.textContent = `${Object.keys(skin.states).length} / 9 可用`;
  ui.builtin.textContent = skin.builtIn ? "自带皮肤" : "我的皮肤";
  ui.builtin.className = `small-badge${skin.builtIn ? "" : " user"}`;
  ui.metaName.value = skin.name;
  ui.metaAuthor.value = skin.author?.name || "";
  ui.metaDescription.value = skin.description || "";

  ui.stateGrid.innerHTML = model.states.map((definition) => {
    const item = skin.states[definition.id];
    const preview = `<img class="state-thumb-image" src="${assetUrl(skin, item.file, item.sha256)}" alt="">`;
    return `
      <article class="state-card ${definition.id === selectedState ? "active" : ""}" data-state="${definition.id}">
        <div class="state-thumb-wrap">
          ${preview}
        </div>
        <div class="state-info">
          <strong>${escapeHtml(definition.label)}</strong>
          <span>${item.frames} 帧 · ${formatBytes(item.bytes)}</span>
          <button class="replace-link" type="button" data-replace="${definition.id}">更换动画</button>
        </div>
      </article>`;
  }).join("");
  freezeThumbnails();
  applyUsagePreview();
}

function render() {
  ui.lowPerformance.checked = lowPerformanceMode;
  renderLibrary();
  renderSelected();
  renderRuntime(model.runtime);
  renderUsage(model.usage, { syncControls: true });
}

ui.lowPerformance.addEventListener("change", () => {
  lowPerformanceMode = ui.lowPerformance.checked;
  window.localStorage.setItem("codex-skin-low-performance", String(lowPerformanceMode));
  renderLibrary();
  renderSelected();
  showToast(lowPerformanceMode ? "低性能模式已开启" : "低性能模式已关闭");
});

async function refresh(preferredId = selectedId) {
  const [data, usage] = await Promise.all([api("/api/bootstrap"), api("/api/usage")]);
  model = { ...data, usage };
  selectedId = data.skins.some((skin) => skin.id === preferredId) ? preferredId : data.skins[0]?.id;
  render();
}

async function uploadGif(file, stateId = selectedState) {
  if (!file || !selectedId) return;
  if (!file.name.toLowerCase().endsWith(".gif")) throw new Error("请选择 GIF 文件");
  setBusy(true, `正在更新${model.states.find((item) => item.id === stateId)?.label || ""}`);
  try {
    const data = await api(`/api/skin/${encodeURIComponent(selectedId)}/state/${encodeURIComponent(stateId)}`, {
      method: "POST",
      headers: { "Content-Type": "image/gif", "X-File-Name": encodeURIComponent(file.name) },
      body: await file.arrayBuffer()
    });
    model.skins = model.skins.map((skin) => skin.id === data.id ? data : skin);
    selectedState = stateId;
    render();
    showToast(`已保留全部 ${data.states[stateId].frames} 帧，文件检查通过`);
  } finally {
    setBusy(false);
    ui.gifInput.value = "";
  }
}

ui.list.addEventListener("click", (event) => {
  const button = event.target.closest("[data-skin]");
  if (!button) return;
  selectedId = button.dataset.skin;
  selectedState = "idle";
  render();
});

ui.stateGrid.addEventListener("click", (event) => {
  const replace = event.target.closest("[data-replace]");
  if (replace) {
    event.stopPropagation();
    selectedState = replace.dataset.replace;
    renderSelected();
    ui.gifInput.click();
    return;
  }
  const card = event.target.closest("[data-state]");
  if (card) {
    selectedState = card.dataset.state;
    renderSelected();
  }
});

ui.search.addEventListener("input", renderLibrary);
ui.usageApiPreset.addEventListener("change", () => {
  if (!USAGE_LINK_ENABLED) return;
  if (ui.usageApiPreset.value !== "custom") ui.usageApiBase.value = ui.usageApiPreset.value;
});
ui.importButton.addEventListener("click", () => ui.importInput.click());
ui.replaceButton.addEventListener("click", () => ui.gifInput.click());
ui.gifInput.addEventListener("change", () => uploadGif(ui.gifInput.files[0]).catch((error) => showToast(error.message, true)));

for (const eventName of ["dragenter", "dragover"]) {
  ui.dropZone.addEventListener(eventName, (event) => {
    event.preventDefault();
    ui.dropZone.classList.add("dragging");
  });
}
for (const eventName of ["dragleave", "drop"]) {
  ui.dropZone.addEventListener(eventName, (event) => {
    event.preventDefault();
    ui.dropZone.classList.remove("dragging");
  });
}
ui.dropZone.addEventListener("drop", (event) => uploadGif(event.dataTransfer.files[0]).catch((error) => showToast(error.message, true)));
ui.dropZone.addEventListener("keydown", (event) => {
  if (event.key !== "Enter" && event.key !== " ") return;
  event.preventDefault();
  ui.gifInput.click();
});

ui.importInput.addEventListener("change", async () => {
  const file = ui.importInput.files[0];
  if (!file) return;
  setBusy(true, "正在安全校验皮肤包");
  try {
    const imported = await api("/api/import", { method: "POST", headers: { "Content-Type": "application/zip" }, body: await file.arrayBuffer() });
    await refresh(imported.id);
    showToast(`已导入 ${imported.name}`);
  } catch (error) {
    showToast(error.message, true);
  } finally {
    setBusy(false);
    ui.importInput.value = "";
  }
});

ui.export.addEventListener("click", () => {
  if (!selectedId) return;
  window.location.href = `/api/export?id=${encodeURIComponent(selectedId)}`;
  showToast("正在准备可分享的皮肤文件");
});

ui.apply.addEventListener("click", async () => {
  if (!selectedId) return;
  setBusy(true, "正在校验并应用皮肤");
  try {
    const result = await api("/api/apply", {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ id: selectedId })
    });
    showToast(`已安装到 ${result.target}，请在 Codex 宠物设置中选择它`);
  } catch (error) {
    showToast(error.message, true);
  } finally {
    setBusy(false);
  }
});

ui.duplicate.addEventListener("click", async () => {
  const skin = selectedSkin();
  if (!skin) return;
  const name = window.prompt("新皮肤名称", `${skin.name} 副本`);
  if (!name) return;
  setBusy(true, "正在创建我的皮肤副本");
  try {
    const created = await api(`/api/skin/${encodeURIComponent(skin.id)}/duplicate`, {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name })
    });
    await refresh(created.id);
    showToast("副本已创建");
  } catch (error) {
    showToast(error.message, true);
  } finally {
    setBusy(false);
  }
});

ui.saveMeta.addEventListener("click", async () => {
  if (!selectedId) return;
  setBusy(true, "正在保存皮肤信息");
  try {
    const updated = await api(`/api/skin/${encodeURIComponent(selectedId)}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: ui.metaName.value, author: ui.metaAuthor.value, description: ui.metaDescription.value })
    });
    model.skins = model.skins.map((skin) => skin.id === updated.id ? updated : skin);
    render();
    showToast("皮肤信息已保存");
  } catch (error) {
    showToast(error.message, true);
  } finally {
    setBusy(false);
  }
});

ui.runtimeSave.addEventListener("click", async () => {
  setBusy(true, "正在检查动画设置");
  try {
    model.runtime = ui.runtimePath.value.trim()
      ? await api("/api/runtime/path", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ path: ui.runtimePath.value.trim() }) })
      : await api("/api/runtime");
    renderRuntime(model.runtime);
    showToast("动画设置检查完成");
  } catch (error) {
    showToast(error.message, true);
  } finally {
    setBusy(false);
  }
});

ui.runtimePatch.addEventListener("click", async () => {
  if (!window.confirm("开启前会自动备份。完成后需要完整退出并重启 Codex，继续吗？")) return;
  setBusy(true, "正在开启完整动画");
  try {
    model.runtime = await api("/api/runtime/patch", {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ path: ui.runtimePath.value.trim() || null })
    });
    renderRuntime(model.runtime);
    showToast("完整动画已开启。请完整退出并重启 Codex");
  } catch (error) {
    showToast(error.message, true);
  } finally {
    setBusy(false);
  }
});

ui.runtimeRestore.addEventListener("click", async () => {
  if (!window.confirm("将关闭完整动画并恢复兼容播放方式，继续吗？")) return;
  setBusy(true, "正在恢复兼容模式");
  try {
    model.runtime = await api("/api/runtime/restore", {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ path: ui.runtimePath.value.trim() || null })
    });
    renderRuntime(model.runtime);
    showToast("已恢复兼容模式，重启 Codex 后生效");
  } catch (error) {
    showToast(error.message, true);
  } finally {
    setBusy(false);
  }
});

ui.usageSave.addEventListener("click", async () => {
  if (!USAGE_LINK_ENABLED) return;
  setBusy(true, "正在保存并检查用量联动");
  try {
    model.usage = await api("/api/usage/settings", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        enabled: ui.usageEnabled.checked,
        apiBaseUrl: ui.usageApiBase.value,
        apiKey: ui.usageKey.value,
        speedEnabled: ui.usageSpeed.checked,
        tintEnabled: ui.usageTint.checked,
        sensitivity: ui.usageSensitivity.value
      })
    });
    ui.usageKey.value = "";
    renderUsage(model.usage, { syncControls: true });
    showToast(model.usage.state.status === "error" ? model.usage.state.message : "用量联动设置已保存");
  } catch (error) {
    showToast(error.message, true);
  } finally {
    setBusy(false);
  }
});

ui.usageClearKey.addEventListener("click", async () => {
  if (!USAGE_LINK_ENABLED) return;
  if (!window.confirm("删除已保存的 Key 并关闭用量联动，继续吗？")) return;
  setBusy(true, "正在删除已保存的 Key");
  try {
    model.usage = await api("/api/usage/key", { method: "DELETE" });
    ui.usageKey.value = "";
    renderUsage(model.usage, { syncControls: true });
    showToast("Key 已删除，用量联动已关闭");
  } catch (error) {
    showToast(error.message, true);
  } finally {
    setBusy(false);
  }
});

async function refreshUsageStatus() {
  if (!USAGE_LINK_ENABLED || !model?.usage?.settings?.enabled) return;
  model.usage = await api("/api/usage");
  renderUsage(model.usage);
}

refresh()
  .then(() => {
    usageRefreshTimer = setInterval(() => refreshUsageStatus().catch(() => {}), 30_000);
  })
  .catch((error) => {
    showToast(error.message, true);
    ui.title.textContent = "载入失败";
    ui.previewMeta.textContent = error.message;
  });

window.addEventListener("beforeunload", () => {
  if (usageRefreshTimer) clearInterval(usageRefreshTimer);
});
