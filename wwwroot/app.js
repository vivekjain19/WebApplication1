// --------------------
// DOM ELEMENTS
// --------------------
const chat = document.getElementById("chat");
const input = document.getElementById("input");
const sendBtn = document.getElementById("send");
const stopBtn = document.getElementById("stop");
const toggleDebug = document.getElementById("toggleDebug");
const debugPanel = document.getElementById("debugPanel");
const debugLog = document.getElementById("debugLog");

// --------------------
// STATE
// --------------------
let controller = null;
let messages = [];
let streaming = false;

// --------------------
// UTIL: KEEP INPUT ALIVE
// --------------------
function keepInputAlive() {
    // Do not steal focus if user clicked elsewhere
    if (document.activeElement !== input) {
        input.focus({ preventScroll: true });
    }
}

// --------------------
// UI EVENTS
// --------------------
toggleDebug.addEventListener("click", () => {
    debugPanel.classList.toggle("hidden");
    keepInputAlive();
});

sendBtn.addEventListener("click", send);

input.addEventListener("keydown", (e) => {
    if (e.key === "Enter") {
        e.preventDefault();
        send();
    }
});

stopBtn.addEventListener("click", () => {
    if (controller) {
        controller.abort();
        controller = null;
        streaming = false;
        logDebug("⛔ Streaming stopped");
        keepInputAlive();
    }
});

// --------------------
// SEND MESSAGE
// --------------------
function send() {
    const text = input.value.trim();
    if (!text || streaming) return;

    input.value = "";
    addMessage(text, "user");
    messages.push({ role: "user", content: text });

    streamResponse();
    keepInputAlive();
}

// --------------------
// ADD MESSAGE
// --------------------
function addMessage(text, role) {
    const wrapper = document.createElement("div");
    wrapper.className = `message ${role}`;

    const content = document.createElement("div");
    content.className = "content";
    content.textContent = text;

    wrapper.appendChild(content);

    if (role === "assistant") {
        const actions = document.createElement("div");
        actions.className = "actions";
        actions.textContent = "Copy";
        actions.addEventListener("click", () => {
            navigator.clipboard.writeText(content.textContent);
            keepInputAlive();
        });
        wrapper.appendChild(actions);
    }

    chat.appendChild(wrapper);
    chat.scrollTop = chat.scrollHeight;

    keepInputAlive();
    return content;
}

// --------------------
// STREAM RESPONSE
// --------------------
async function streamResponse() {
    controller = new AbortController();
    streaming = true;

    const contentDiv = addMessage("", "assistant");

    let response;
    try {
        response = await fetch("/api/chat/stream", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ messages }),
            signal: controller.signal
        });
    } catch (err) {
        contentDiv.textContent = "❌ Failed to connect.";
        streaming = false;
        keepInputAlive();
        return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    try {
        while (true) {
            const { value, done } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split(/\r?\n/);
            buffer = lines.pop();

            for (const line of lines) {
                handleLine(line, contentDiv);
            }

            keepInputAlive();
        }
    } catch (err) {
        // ignore abort errors
    }

    messages.push({
        role: "assistant",
        content: contentDiv.textContent
    });

    streaming = false;
    controller = null;
    keepInputAlive();
}

// --------------------
// HANDLE STREAM LINE
// --------------------
function handleLine(line, contentDiv) {
    if (!line) return;

    if (line.startsWith("debug:")) {
        logDebug("ℹ️ " + line.slice(6));
        return;
    }

    if (line.startsWith("tool:")) {
        logDebug("🛠 " + line.slice(5));
        return;
    }

    if (line.startsWith("text:")) {
        const clean = line.replace(/^text:/g, "").replace(/text:/g, "");
        contentDiv.textContent += clean;
        chat.scrollTop = chat.scrollHeight;
    }
}

// --------------------
// DEBUG
// --------------------
function logDebug(msg) {
    debugLog.textContent += msg + "\n";
    debugLog.scrollTop = debugLog.scrollHeight;
    keepInputAlive();
}

// --------------------
// INITIAL FOCUS
// --------------------
window.addEventListener("load", () => {
    input.focus();
});
