import { createCanvas } from '@napi-rs/canvas'
import { readFile, writeFile } from 'node:fs/promises'
import { boundsToRect, getPathBounds, parsePath, segmentLength2 } from './path.js'

const start = async () => {
  // get the path and metrics, log them
  const rawPath = await readFile( './data/nighttrain_path.txt', 'utf8')

  const path = parsePath(rawPath)

  console.log('Parsed path with', path.length, 'records')

  const bounds = getPathBounds(path)

  console.log('Path bounds:', bounds)

  const rect = boundsToRect(bounds)

  console.log('Path rect:', rect)

  if( path.length < 2 ){
    console.error('Path has less than 2 points, cannot render segments')
    
    return
  }

  // analyze segment lengths, log and write to a text file
  const segmentLengths: number[] = []

  for( let i = 0; i < path.length; i++ ){
    const len = segmentLength2( path, i )
  
    segmentLengths.push( len )
  }

  const totalLength = segmentLengths.reduce( (a,b) => a + b, 0 )

  const avgLength = totalLength / segmentLengths.length

  console.log('Total path length:', totalLength.toFixed(2))
  console.log('Average segment length:', avgLength.toFixed(2))

  // one seg length per line
  const segmentLengthsText = segmentLengths.map( l => l.toFixed(2) ).join('\n')
  
  await writeFile(
    './data/nighttrain_path_segment_lengths.txt', segmentLengthsText, 'utf8'
  )

  // draw debug image

  let { width, height } = rect

  width = Math.ceil(width)
  height = Math.ceil(height)

  const padding = 100
  const pathWidth = 10
  const pointRadius = 20

  const canvasWidth = width + padding * 2
  const canvasHeight = height + padding * 2

  const canvas = createCanvas(canvasWidth, canvasHeight)
  const ctx = canvas.getContext('2d')

  ctx.fillStyle = 'white'
  ctx.fillRect(0, 0, canvasWidth, canvasHeight)

  // when converting the source data to 2d, East and West are correct but 
  // North and South are flipped, so flip the Y axis

  ctx.translate(0, canvasHeight)
  ctx.scale(1, -1)

  ctx.lineWidth = pathWidth
  
  // Helper to map a 0..1 value to a hue from blue (240deg) to red (0deg)
  const hueForT = (t: number) => 240 * (1 - t)
  const colorForT = (t: number) => `hsl(${hueForT(t)}, 100%, 50%)`

  // Draw per-segment with its own gradient so colors step smoothly from blue to red.
  // Define segments as consecutive pairs without closing the loop, so the first
  // segment starts at pure blue and the last ends at pure red.
  const segmentCount = Math.max(0, path.length - 1)

  for (let i = 0; i < segmentCount; i++) {
    const p0 = path[i]
    const p1 = path[i + 1]

    const x0 = p0.x - rect.x + padding
    const y0 = p0.y - rect.y + padding
    const x1 = p1.x - rect.x + padding
    const y1 = p1.y - rect.y + padding

    // Fraction across the entire path for start/end of this segment
    const t0 = segmentCount > 1 ? i / (segmentCount - 1) : 0
    const t1 = segmentCount > 1 ? (i + 1) / (segmentCount - 1) : 1

    const grad = ctx.createLinearGradient(x0, y0, x1, y1)
    grad.addColorStop(0, colorForT(t0))
    grad.addColorStop(1, colorForT(t1))

    // draw segment

    ctx.strokeStyle = grad
    ctx.beginPath()
    ctx.moveTo(x0, y0)
    ctx.lineTo(x1, y1)
    ctx.stroke()
    ctx.closePath()

    // draw point at start of segment

    ctx.fillStyle = colorForT(t0)
    ctx.beginPath()
    ctx.arc(x0, y0, pointRadius, 0, Math.PI * 2)
    ctx.fill()
    ctx.closePath()
  }

  // close the path by drawing from last to first

  ctx.strokeStyle = colorForT(0) // blue
  {
    const p0 = path[path.length - 1]
    const p1 = path[0]

    ctx.beginPath()
    ctx.moveTo(p0.x - rect.x + padding, p0.y - rect.y + padding)
    ctx.lineTo(p1.x - rect.x + padding, p1.y - rect.y + padding)
    ctx.stroke()
    ctx.closePath()
  }

  const bytes = canvas.toBuffer('image/png')

  await writeFile('./data/nighttrain_path.png', bytes)
}

start().catch(console.error)
