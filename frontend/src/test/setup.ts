import { notifyManager } from '@tanstack/react-query'
import { act } from '@testing-library/react'
import '@testing-library/jest-dom'

notifyManager.setNotifyFunction((callback) => {
  act(callback)
})
