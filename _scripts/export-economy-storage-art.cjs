// Export only: uniform downsampling, transparent framing and a labeled review sheet.
// No mirroring, rotation, recoloring, background removal or structural repainting.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '..');
const evidence = path.join(root, 'docs/验证/共有私有仓储美术_20261010');
const textureDir = path.join(root, 'Textures/Things/Building/PawnEconomy');
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex').toUpperCase();
function bounds(data, width, height, threshold) {
  let x0=width, y0=height, x1=-1, y1=-1;
  for (let y=0;y<height;y++) for (let x=0;x<width;x++) {
    if(data[(y*width+x)*4+3] >= threshold) { x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y); }
  }
  if(x1<0) throw new Error('Empty sprite');
  return {left:x0,top:y0,width:x1-x0+1,height:y1-y0+1};
}
function components(data,width,height) {
  const visited=new Uint8Array(width*height), queue=new Int32Array(width*height), sizes=[];
  for(let start=0;start<visited.length;start++) {
    if(visited[start]||data[start*4+3]<32) continue;
    let read=0,write=1;queue[0]=start;visited[start]=1;
    while(read<write) {
      const point=queue[read++],x=point%width,y=Math.floor(point/width);
      for(const next of [x>0?point-1:-1,x+1<width?point+1:-1,y>0?point-width:-1,y+1<height?point+width:-1])
        if(next>=0&&!visited[next]&&data[next*4+3]>=32) {visited[next]=1;queue[write++]=next;}
    }
    sizes.push(write);
  }
  return sizes.sort((a,b)=>b-a);
}
(async () => {
  const sources = JSON.parse(fs.readFileSync(path.join(evidence, 'selected-sources.json'),'utf8').replace(/^\uFEFF/,''));
  if(sources.length !== 8 || new Set(sources.map(s=>s.name)).size!==8) throw new Error('Exactly eight distinct directional assets required');
  fs.mkdirSync(textureDir,{recursive:true});
  const records=[], composite=[];
  for(const source of sources) {
    const sourcePath=path.join(evidence,source.source);
    const meta=await sharp(sourcePath).metadata();
    if(meta.format!=='png'||!meta.hasAlpha) throw new Error(`${source.name}: transparent PNG required`);
    const {data,info}=await sharp(sourcePath).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    // Ignore sub-3.2% alpha encoding noise when finding the visible crop; keep alpha
    // values unchanged inside that rectangular crop and preserve edge antialias.
    const edge=bounds(data,info.width,info.height,8), solid=bounds(data,info.width,info.height,128);
    if(edge.left===0||edge.top===0||edge.left+edge.width===info.width||edge.top+edge.height===info.height) throw new Error(`${source.name}: clipped or nontransparent canvas edge`);
    if(Math.abs(edge.left-solid.left)>6||Math.abs(edge.top-solid.top)>6||Math.abs(edge.width-solid.width)>12||Math.abs(edge.height-solid.height)>12) throw new Error(`${source.name}: diffuse alpha halo`);
    const vertical=source.name.endsWith('_east')||source.name.endsWith('_west');
    const ratio=(vertical?solid.height/solid.width:solid.width/solid.height);
    // The storage cells remain 2:1; bracket/rail overhang can vary the visible silhouette.
    const edgeRatioTolerance=2*ratio/(vertical?solid.width:solid.height);
    if(ratio<1.7-edgeRatioTolerance||ratio>2.3+edgeRatioTolerance) throw new Error(`${source.name}: footprint aspect ${ratio.toFixed(3)} outside rack silhouette tolerance`);
    // Normalize the long axis to two cells. Hasp/hinge overhang remains visible on
    // the short axis instead of shrinking a whole direction to fit a one-cell box.
    const bodyWidth=vertical?96:128, bodyHeight=vertical?128:96;
    const canvasWidth=vertical?128:192, canvasHeight=vertical?192:128;
    const scaled=await sharp(sourcePath).extract(edge).resize({width:bodyWidth,height:bodyHeight,fit:'inside',kernel:'lanczos3'}).png().toBuffer();
    const size=await sharp(scaled).metadata();
    const file=path.join(textureDir,source.name+'.png');
    await sharp({create:{width:canvasWidth,height:canvasHeight,channels:4,background:'#00000000'}})
      .composite([{input:scaled,left:Math.floor((canvasWidth-size.width)/2),top:Math.floor((canvasHeight-size.height)/2)}]).png().toFile(file);
    const output=await sharp(file).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    const outputBounds=bounds(output.data,canvasWidth,canvasHeight,1);
    const connected=components(output.data,canvasWidth,canvasHeight);
    if(connected.slice(1).some(size=>size>=4)) throw new Error(`${source.name}: disconnected visible fragment in final export`);
    for(let x=0;x<canvasWidth;x++) if(output.data[x*4+3]!==0||output.data[((canvasHeight-1)*canvasWidth+x)*4+3]!==0) throw new Error('Nontransparent top/bottom border');
    for(let y=0;y<canvasHeight;y++) if(output.data[(y*canvasWidth)*4+3]!==0||output.data[(y*canvasWidth+canvasWidth-1)*4+3]!==0) throw new Error('Nontransparent left/right border');
    let transparent=0;
    for(let i=3;i<output.data.length;i+=4) if(output.data[i]===0) transparent++;
    const relative=path.relative(root,file).replaceAll('\\','/');
    records.push({path:relative,source:source.source,sourceSha256:hash(sourcePath),sha256:hash(file),width:canvasWidth,height:canvasHeight,sourceBounds:edge,opaqueSourceBounds:solid,sourceAspect:ratio,edgeRatioTolerance,cropAlphaThreshold:8,exportBounds:outputBounds,connectedComponentSizes:connected,borderFullyTransparent:true,transparentFraction:transparent/(canvasWidth*canvasHeight),format:'RGBA PNG',export:'visible rectangular crop, uniform resize and transparent framing only; retained alpha unchanged',visualReviewed:false,gameVerified:false});
    const index=records.length-1, column=index%4, row=Math.floor(index/4);
    const enlarged=await sharp(file).resize(canvasWidth*2,canvasHeight*2,{kernel:'nearest'}).png().toBuffer();
    composite.push({input:enlarged,left:column*400+Math.floor((400-canvasWidth*2)/2),top:row*430+48+Math.floor((340-canvasHeight*2)/2)});
  }
  const labels=sources.map((s,i)=>`<text x="${i%4*400+200}" y="${Math.floor(i/4)*430+32}" text-anchor="middle" fill="#e6e9ee" font-family="Arial" font-size="18">${s.name}</text>`).join('');
  composite.push({input:Buffer.from(`<svg width="1600" height="860" xmlns="http://www.w3.org/2000/svg">${labels}</svg>`),left:0,top:0});
  await sharp({create:{width:1600,height:860,channels:4,background:'#272b2e'}}).composite(composite).png().toFile(path.join(evidence,'asset-review.png'));
  const lightLabels=sources.map((s,i)=>`<text x="${i%4*400+200}" y="${Math.floor(i/4)*430+32}" text-anchor="middle" fill="#282c2f" font-family="Arial" font-size="18">${s.name}</text>`).join('');
  const lightComposite=composite.slice(0,-1).concat([{input:Buffer.from(`<svg width="1600" height="860" xmlns="http://www.w3.org/2000/svg">${lightLabels}</svg>`),left:0,top:0}]);
  await sharp({create:{width:1600,height:860,channels:4,background:'#d9d9ce'}}).composite(lightComposite).png().toFile(path.join(evidence,'asset-review-light.png'));
  fs.writeFileSync(path.join(evidence,'asset-manifest.json'),JSON.stringify({changeId:'ART-20261010-ECONOMY-STORAGE',tool:'built-in image_gen',files:records,technicalChecksPassed:true,visualReviewed:false,gameVerified:false},null,2)+'\n');
  process.stdout.write(`PASS: ${records.length} transparent direction exports; no rotation or recoloring.\n`);
})().catch(error=>{process.stderr.write(error.stack+'\n');process.exitCode=1;});
