/**
 * fetch_links.mjs - 抓取网页并提取所有链接（零依赖，纯 Node.js 内置模块）
 * 用法: node scripts/fetch_links.mjs <url> [--proxy <proxy>] [--all-domains]
 */

import http from "node:http";
import https from "node:https";
import { URL } from "node:url";

const PROXY_DEFAULT = "http://127.0.0.1:10809";

function parseArgs() {
  const args = process.argv.slice(2);
  const result = { url: null, proxy: PROXY_DEFAULT, allDomains: false };
  for (let i = 0; i < args.length; i++) {
    if (args[i] === "--proxy" && args[i + 1]) result.proxy = args[++i];
    else if (args[i] === "--all-domains") result.allDomains = true;
    else if (!args[i].startsWith("--")) result.url = args[i];
  }
  if (!result.url) {
    console.error("用法: node scripts/fetch_links.mjs <url> [--proxy <proxy>] [--all-domains]");
    process.exit(1);
  }
  return result;
}

function fetchViaProxy(targetUrl, proxyUrl) {
  return new Promise((resolve, reject) => {
    const target = new URL(targetUrl);
    const proxy = new URL(proxyUrl);

    // 通过代理建立 CONNECT 隧道
    const req = http.request({
      host: proxy.hostname,
      port: proxy.port,
      method: "CONNECT",
      path: `${target.hostname}:443`,
    });

    req.on("connect", (res, socket) => {
      if (res.statusCode !== 200) {
        reject(new Error(`代理连接失败: ${res.statusCode}`));
        return;
      }

      // 通过隧道发送 HTTPS 请求
      const tlsOptions = {
        hostname: target.hostname,
        path: target.pathname + target.search,
        method: "GET",
        socket: socket,
        agent: false,
        headers: {
          "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
          "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
          "Accept-Language": "zh-CN,zh;q=0.9,en;q=0.8",
          "Host": target.hostname,
        },
      };

      const tlsReq = https.request(tlsOptions, (tlsRes) => {
        // 处理重定向
        if (tlsRes.statusCode >= 300 && tlsRes.statusCode < 400 && tlsRes.headers.location) {
          const redirectUrl = new URL(tlsRes.headers.location, targetUrl).href;
          fetchViaProxy(redirectUrl, proxyUrl).then(resolve).catch(reject);
          return;
        }

        if (tlsRes.statusCode !== 200) {
          reject(new Error(`HTTP ${tlsRes.statusCode}`));
          return;
        }

        const chunks = [];
        tlsRes.on("data", (chunk) => chunks.push(chunk));
        tlsRes.on("end", () => resolve(Buffer.concat(chunks).toString("utf-8")));
        tlsRes.on("error", reject);
      });

      tlsReq.on("error", reject);
      tlsReq.end();
    });

    req.on("error", reject);
    req.end();
  });
}

function fetchDirect(targetUrl) {
  return new Promise((resolve, reject) => {
    const mod = targetUrl.startsWith("https") ? https : http;
    const req = mod.get(targetUrl, {
      headers: {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
      },
    }, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        fetchDirect(new URL(res.headers.location, targetUrl).href).then(resolve).catch(reject);
        return;
      }
      if (res.statusCode !== 200) { reject(new Error(`HTTP ${res.statusCode}`)); return; }
      const chunks = [];
      res.on("data", (chunk) => chunks.push(chunk));
      res.on("end", () => resolve(Buffer.concat(chunks).toString("utf-8")));
      res.on("error", reject);
    });
    req.on("error", reject);
  });
}

function extractLinks(html, baseUrl) {
  const seen = new Set();
  const links = [];
  const re = /<a\s[^>]*?href\s*=\s*["']([^"'#]*?)["'][^>]*?>/gi;
  let m;
  while ((m = re.exec(html)) !== null) {
    let href = m[1].trim();
    if (!href || href.startsWith("javascript:") || href.startsWith("mailto:")) continue;
    let fullUrl;
    try { fullUrl = new URL(href, baseUrl).href; } catch { continue; }
    if (!fullUrl.startsWith("http://") && !fullUrl.startsWith("https://")) continue;
    fullUrl = fullUrl.split("#")[0];
    if (seen.has(fullUrl)) continue;
    seen.add(fullUrl);
    const tagEnd = html.indexOf(">", m.index);
    const nextA = html.indexOf("</a>", tagEnd);
    const text = (tagEnd > 0 && nextA > tagEnd) ? html.slice(tagEnd + 1, nextA).replace(/<[^>]+>/g, "").trim().slice(0, 100) : "";
    links.push({ url: fullUrl, text });
  }
  return links;
}

async function main() {
  const { url, proxy, allDomains } = parseArgs();
  console.error(`正在抓取: ${url}`);
  if (proxy) console.error(`使用代理: ${proxy}`);

  let html;
  try {
    html = proxy ? await fetchViaProxy(url, proxy) : await fetchDirect(url);
  } catch (e) {
    console.error(`抓取失败: ${e.message}`);
    process.exit(1);
  }

  console.error(`页面大小: ${html.length} 字节`);

  let links = extractLinks(html, url);
  console.error(`提取链接总数: ${links.length}`);

  if (!allDomains) {
    const baseDomain = new URL(url).hostname;
    links = links.filter(l => { try { return new URL(l.url).hostname === baseDomain; } catch { return false; } });
    console.error(`同域名链接: ${links.length}`);
  }

  console.log(JSON.stringify(links, null, 2));
}

main();
