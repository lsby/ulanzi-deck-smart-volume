const WebSocket = require('ws');
const fs = require('fs');
const path = require('path');
const { exec, execFile } = require('child_process');
const net = require('net');

const configPath = path.join(__dirname, 'config.json');
let config = { debug: false };
try {
    if (fs.existsSync(configPath)) {
        config = Object.assign(config, JSON.parse(fs.readFileSync(configPath, 'utf8')));
    }
} catch (e) {
    // 忽略配置文件读取错误
}

const logDir = path.join(__dirname, 'logs');
const logFile = path.join(logDir, 'plugin.log');
function log(msg) {
    if (!config.debug) return;
    try {
        if (!fs.existsSync(logDir)) {
            fs.mkdirSync(logDir, { recursive: true });
        }
        fs.appendFileSync(logFile, `[${new Date().toISOString()}] ${msg}\n`);
    } catch (e) {
        // 忽略日志写入错误
    }
}

log("Plugin started with args: " + process.argv.join(" "));

// Ulanzi passes arguments directly without flags: [address, port, language]
const address = process.argv[2] || '127.0.0.1';
const port = process.argv[3];
const language = process.argv[4] || 'en';
const uuid = 'com.lsby.smart-volume'; // 对应 manifest.json 中的 UUID

if (!port) {
    log("Missing port argument.");
    process.exit(1);
}

const svvPath = path.join(__dirname, 'bin', 'SoundVolumeView.exe');
const getActiveAppPath = path.join(__dirname, 'bin', 'get-active-app.exe');
const getAudioSessionsPath = path.join(__dirname, 'bin', 'get-audio-sessions.exe');
const osdPath = path.join(__dirname, 'bin', 'volume-osd.exe');

function triggerVolumeCmd(targetMode, direction, context) {
    const step = 2;
    const change = direction.includes('right') ? `+${step}` : `-${step}`;
    
    const pipeName = '\\\\.\\pipe\\UlanziSmartVolumeOSD';
    const contextStr = context ? `|${context}` : '|default';
    const msg = `CMD|${targetMode}|${change}${contextStr}\r\n`;
    
    const client = net.connect(pipeName, () => {
        client.write(msg);
        client.end();
    });
    client.on('error', () => {
        // 如果管道不存在（程序没开），就直接用命令行拉起，并传入当前 Node.js 的 PID 用于进程绑定
        const spawn = require('child_process').spawn;
        const child = spawn(`"${osdPath}"`, ["CMD", targetMode, change, process.pid.toString()], {
            detached: true,
            shell: true,
            windowsHide: false
        });
        child.unref();
    });
}

const ws = new WebSocket(`ws://${address}:${port}`);

let lastDialDownTime = new Map();
let singleClickTimeout = new Map();
let holdTimeout = null;

ws.on('open', () => {
    log("Connected to Ulanzi Deck WebSocket.");
    const registerJson = {
        code: 0,
        cmd: 'connected',
        uuid: uuid
    };
    ws.send(JSON.stringify(registerJson));
    
    // 静默预热 OSD 进程，绑定生命周期
    const spawn = require('child_process').spawn;
    const child = spawn(`"${osdPath}"`, ["CMD", "Preload", "0", process.pid.toString()], {
        detached: true,
        shell: true,
        windowsHide: false
    });
    child.unref();
});

ws.on('message', (data) => {
    try {
        const msg = JSON.parse(data.toString());
        log(`[WS] 收到消息: cmd=${msg.cmd}, actionid=${msg.actionid}, event=${msg.event}, context=${msg.context}, payload=${JSON.stringify(msg.payload)}`);
        const ctx = msg.actionid || msg.context || 'default';
        
        if (msg.cmd === 'dialrotate') {
            if (msg.rotateEvent.includes('hold')) {
                log(`[ACTION] 旋钮按下时扭动: 方向=${msg.rotateEvent}, context=${ctx}`);
                triggerVolumeCmd('Foreground', msg.rotateEvent, ctx);
            } else {
                log(`[ACTION] 旋钮正常扭动: 方向=${msg.rotateEvent}, context=${ctx}`);
                triggerVolumeCmd('Master', msg.rotateEvent, ctx);
            }
        } else if (msg.cmd === 'dialdown') {
            const now = Date.now();
            log(`[ACTION] 旋钮被按下: context=${ctx}`);
            
            if (singleClickTimeout.has(ctx)) {
                clearTimeout(singleClickTimeout.get(ctx));
                singleClickTimeout.delete(ctx);
            }

            const lastDown = lastDialDownTime.get(ctx) || 0;
            if (now - lastDown < 400) {
                log(`[ACTION] 旋钮双击: context=${ctx}`);
                triggerVolumeCmd('ToggleListMode', 'right', ctx);
                lastDialDownTime.set(ctx, 0);
            } else {
                lastDialDownTime.set(ctx, now);
            }
        } else if (msg.cmd === 'dialup') {
            const now = Date.now();
            log(`[ACTION] 旋钮被松开: context=${ctx}`);
            
            const lastDown = lastDialDownTime.get(ctx) || 0;
            if (lastDown !== 0) {
                const timeSinceDown = now - lastDown;
                // 单击必须在400ms的判定窗口结束后才执行，以防止被双击抢占
                const delay = Math.max(0, 400 - timeSinceDown);
                singleClickTimeout.set(ctx, setTimeout(() => {
                    log(`[ACTION] 旋钮单击: context=${ctx}`);
                    triggerVolumeCmd('SingleClick', 'right', ctx);
                    singleClickTimeout.delete(ctx);
                    lastDialDownTime.set(ctx, 0);
                }, delay));
            }
        } else if (msg.cmd === 'sendToPlugin' && msg.payload) {
            const payload = msg.payload;
            const actionUuid = msg.uuid;
            const actionContext = msg.actionid || msg.context || ctx;
            
            if (payload.command === 'get_active_processes') {
                execFile(getAudioSessionsPath, (err, stdout) => {
                    if (err) return;
                    const apps = stdout.trim().split('\n').map(s => s.trim()).filter(s => s);
                    let currentDefault = '';
                    const defaultAppsFile = path.join(__dirname, 'default_apps.txt');
                    if (fs.existsSync(defaultAppsFile)) {
                        const lines = fs.readFileSync(defaultAppsFile, 'utf-8').split('\n');
                        for (const line of lines) {
                            const [k, v] = line.split('=');
                            if (k === actionContext && v) currentDefault = v.trim();
                        }
                    }
                    ws.send(JSON.stringify({
                        cmd: 'sendToPropertyInspector',
                        uuid: actionUuid,
                        actionid: actionContext,
                        payload: {
                            type: 'active_processes',
                            apps: apps,
                            currentDefault: currentDefault
                        }
                    }));
                });
            } else if (payload.command === 'set_default_app') {
                const appName = payload.appName;
                const defaultAppsFile = path.join(__dirname, 'default_apps.txt');
                let dict = {};
                if (fs.existsSync(defaultAppsFile)) {
                    const lines = fs.readFileSync(defaultAppsFile, 'utf-8').split('\n');
                    for (const line of lines) {
                        const [k, v] = line.split('=');
                        if (k && v) dict[k] = v.trim();
                    }
                }
                if (appName && appName !== '系统主音量') {
                    dict[actionContext] = appName;
                } else {
                    delete dict[actionContext];
                }
                const outLines = Object.entries(dict).map(([k, v]) => `${k}=${v}`);
                fs.writeFileSync(defaultAppsFile, outLines.join('\n'));
                
                // 通知 OSD 热重载配置
                const pipeName = '\\\\.\\pipe\\UlanziSmartVolumeOSD';
                const reloadMsg = `CMD|ReloadConfig|0|default\r\n`;
                const reloadClient = net.connect(pipeName, () => {
                    reloadClient.write(reloadMsg);
                    reloadClient.end();
                });
                reloadClient.on('error', () => { /* 忽略错误 */ });
            }
        }
    } catch (e) {
        log("Error parsing message: " + e.message);
    }
});

ws.on('close', () => {
    log("WebSocket closed.");
    process.exit(0);
});

ws.on('error', (err) => {
    log("WebSocket error: " + err.message);
});
