import { describe, expect, it } from 'vitest'
import { slugify, toCategoryInput, validateCategory } from './categoryFormValidation'

describe('category form rules', () => {
  it('normalizes accents, spaces and incompatible characters', () => expect(slugify(' Fuerza Máxima / Pro ')).toBe('fuerza-maxima-pro'))
  it('rejects required and invalid numeric fields', () => expect(validateCategory({ name: '', slug: '', description: '', displayOrder: '-1' })).toEqual(expect.objectContaining({ name: expect.any(String), slug: expect.any(String), displayOrder: expect.any(String) })))
  it('creates a trimmed API payload', () => expect(toCategoryInput({ name: ' Fuerza ', slug: 'Fuerza Total', description: ' Equipo ', displayOrder: '2' })).toEqual({ name: 'Fuerza', slug: 'fuerza-total', description: 'Equipo', displayOrder: 2 }))
})
