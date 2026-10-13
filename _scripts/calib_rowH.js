/* 标定：二分找出使整行 == 18px 的 td line-height */
const puppeteer = require('puppeteer');
const FILE = 'E:/SteamLibrary/steamapps/common/RimWorld/11/殖民地综合管理系统/系统中心蓝图/市场经济框架_玩家预览_RUIF单页统一版.html';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';

(async () => {
  const browser = await puppeteer.launch({
    headless: 'new', executablePath: CHROME,
    args: ['--no-sandbox', '--disable-dev-shm-usage', '--font-render-hinting=none']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 1600, height: 1000 });
  await page.goto('file:///' + FILE.replace(/\\/g, '/'), { waitUntil: 'networkidle0' });
  await new Promise(r => setTimeout(r, 300));

  const results = [];
  for (const lh of [13, 14, 15, 16, 17, 18]) {
    const h = await page.evaluate((lhVal) => {
      let st = document.getElementById('__probe');
      if (!st) { st = document.createElement('style'); st.id = '__probe'; document.head.appendChild(st); }
      st.textContent = `.dt tbody td{line-height:${lhVal}px !important}`;
      const tr = document.querySelector('table.dt tbody tr');
      return tr.getBoundingClientRect().height;
    }, lh);
    results.push({ lh, h });
  }
  console.log('td line-height -> 整行高度');
  for (const r of results) {
    console.log(`  line-height:${r.lh}px  =>  ${r.h}px  ${r.h === 18 ? '  <== 目标 18' : ''}`);
  }
  await browser.close();
})();
