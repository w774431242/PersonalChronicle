// 迷你 XML 解析器（Tools/DataSimulator 专用）。
// 目标：解析项目 Defs 目录下规整缩进 XML（无属性、无混合内容、无 CDATA）。
// 输出：{ tag, children: [], text } 树；不支持 XML 属性（Defs 未使用）。
'use strict';

function parseXml(text) {
  if (typeof text !== 'string' || text.length === 0) {
    throw new Error('xml: empty input');
  }
  // 去掉注释与 XML 声明
  let src = text.replace(/<!--[\s\S]*?-->/g, '');
  src = src.replace(/<\?xml[\s\S]*?\?>/g, '');

  let i = 0;

  function skipWs() {
    while (i < src.length && /\s/.test(src[i])) i++;
  }

  function parseNode() {
    skipWs();
    if (i >= src.length || src[i] !== '<') {
      throw new Error('xml: unexpected content at offset ' + i);
    }
    i++; // consume '<'
    let tag = '';
    while (i < src.length && src[i] !== '>' && !/\s/.test(src[i])) {
      tag += src[i];
      i++;
    }
    if (tag.length === 0) throw new Error('xml: empty tag at offset ' + i);
    // 跳过属性区（本项目无属性，直接到 '>'）
    while (i < src.length && src[i] !== '>') i++;
    i++; // consume '>'

    const children = [];
    let textContent = '';
    while (i < src.length) {
      const ch = src[i];
      if (ch === '<') {
        if (src[i + 1] === '/') {
          // 闭合标签
          i += 2;
          while (i < src.length && src[i] !== '>') i++;
          i++; // consume '>'
          return { tag, children, text: textContent };
        }
        if (src[i + 1] === '!') {
          // 注释残留（防御）
          const end = src.indexOf('-->', i);
          i = end < 0 ? src.length : end + 3;
          continue;
        }
        children.push(parseNode());
      } else {
        textContent += ch;
        i++;
      }
    }
    return { tag, children, text: textContent };
  }

  const root = parseNode();
  return root;
}

module.exports = { parseXml };
