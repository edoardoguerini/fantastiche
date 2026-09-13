import { expect, it } from 'vitest'
import { initials } from '../initials'

it('usa le iniziali delle prime due parole', () => {
  expect(initials('Serie Amici 2026/27')).toBe('SA')
  expect(initials('  real   madrigale ')).toBe('RM')
})

it('usa le prime due lettere di una parola sola', () => {
  expect(initials('Fenici')).toBe('FE')
  expect(initials('x')).toBe('X')
})

it('restituisce una stringa vuota senza testo', () => {
  expect(initials('   ')).toBe('')
})
