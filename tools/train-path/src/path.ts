import { trim, notEmpty } from './util.js'

export type Vector2 = {
  x: number
  y: number
}

export type Vector3 = Vector2 & {
  z: number
}

// GTA V - 
// x is east-west
// y is north-south
// z is up-down
// heading is degrees - 0 north, 90 east, 180 south, 270 west etc
export type TrainPathPosition = Vector3 & {
  heading: number
}

// fields are space separated, records are newline separated
export const parsePath = (text: string): TrainPathPosition[] => {
  text = text.trim()

  const lines = text.split('\n').map(trim).filter(notEmpty)

  const records = lines.map((line, i) => {
    const fields = line.split(' ').map(trim).filter(notEmpty)

    if (fields.length !== 4) {
      throw Error(`Invalid record at line ${i + 1}: ${line}`)
    }

    const [x, y, z, heading] = fields.map(Number)

    return { x, y, z, heading }
  })

  return records
}

const wrapIndex = ( length: number, index: number ) => {
  while( index < 0 ) index += length
  while( index >= length ) index -= length

  return index
}
  

export const segmentLength2 = ( path: TrainPathPosition[], index: number ) => {
  index = wrapIndex( path.length, index )

  const nextIndex = wrapIndex( path.length, index + 1 )

  const p0 = path[index]
  const p1 = path[nextIndex]

  const dx = p1.x - p0.x
  const dy = p1.y - p0.y

  return Math.sqrt(dx * dx + dy * dy)
}

export type Bounds2 = {
  minX: number
  maxX: number
  minY: number
  maxY: number
}

export type Bounds3 = Bounds2 & {
  minZ: number
  maxZ: number
}

export const getPathBounds = (path: TrainPathPosition[]): Bounds3 => {
  let minX = Infinity
  let maxX = -Infinity
  let minY = Infinity
  let maxY = -Infinity
  let minZ = Infinity
  let maxZ = -Infinity

  for (const { x, y, z } of path) {
    if (x < minX) minX = x
    if (x > maxX) maxX = x
    if (y < minY) minY = y
    if (y > maxY) maxY = y
    if (z < minZ) minZ = z
    if (z > maxZ) maxZ = z
  }

  return { minX, maxX, minY, maxY, minZ, maxZ }
}

export type Rect = {
  x: number
  y: number
  width: number
  height: number
}

export const boundsToRect = (bounds: Bounds2): Rect => ({
  x: bounds.minX,
  y: bounds.minY,
  width: bounds.maxX - bounds.minX,
  height: bounds.maxY - bounds.minY
})
