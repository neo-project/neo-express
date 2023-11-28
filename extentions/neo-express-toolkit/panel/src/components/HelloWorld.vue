<script setup>
import { ref } from 'vue'

defineProps({
  msg: String,
})

const count = ref(0)

function sayHello(event)
{
  vscode.postMessage({
    message: 'extension can listen to vue events by using postMessage method!'
  })
}
</script>

<template>
  <h1>{{ msg }}</h1>
  
  <button @click="sayHello">Say Hello</button>
</template>

<style scoped>
.read-the-docs {
  color: #888;
}
</style>
