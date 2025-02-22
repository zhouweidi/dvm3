package main

import (
	"fmt"
	"math"
	"math/rand"
	"runtime"
	"time"
)

type MESSAGE_TYPE int

const (
	MessageGreeting MESSAGE_TYPE = iota
	MessageAck
	MessageSchedule
)

func (msg MESSAGE_TYPE) String() string {
	switch msg {
	case MessageGreeting:
		return "Greeting"
	case MessageAck:
		return "Ack"
	case MessageSchedule:
		return "Schedule"
	default:
		return "Unknown"
	}
}

type Message struct {
	message   MESSAGE_TYPE
	from      int
	timestamp time.Time
}

func NewMessage(message MESSAGE_TYPE) Message {
	return Message{
		message:   message,
		from:      -1,
		timestamp: time.Now(),
	}
}

type Ant struct {
	vid               int
	ch                chan Message
	startTime         time.Time
	startCompleteTime time.Time
	greetingsSent     int
	greetingsReceived int
	AcksReceived      int
	greetingRTTs      []int64
	random            rand.Rand
}

func NewAnt(vid int) *Ant {
	return &Ant{
		vid:               vid,
		ch:                make(chan Message),
		random:            *rand.New(rand.NewSource(rand.Int63())),
		startTime:         time.Now(),
		startCompleteTime: time.Time{},
	}
}

func (ant *Ant) Run() {
	for {
		msg := <-ant.ch
		// fmt.Printf("Received %s: %d -> %d\n", msg.message.String(), msg.from, ant.vid)
		switch msg.message {
		case MessageGreeting:
			ant.SendAck(msg.from)
			ant.greetingsReceived++
			ant.SendGreeting()
		case MessageAck:
			ant.AcksReceived++
			ant.greetingRTTs = append(ant.greetingRTTs, int64(time.Now().Sub(msg.timestamp)))
		case MessageSchedule:
			ant.startCompleteTime = time.Now()
			ant.SendGreeting()
		}
	}
}

func (ant *Ant) Send(dst int, msg Message) {
	//	fmt.Printf("Send %s: %d -> %d\n", msg.message.String(), ant.vid, dst)
	msg.from = ant.vid
	Ants[dst].ch <- msg
	if msg.message == MessageGreeting {
		ant.greetingsSent++
	}
}

func (ant *Ant) SendGreeting() {
	var target int
	for {
		target = ant.GetGreetingTarget()
		if target != ant.vid {
			break
		}
	}

	go ant.Send(target, NewMessage(MessageGreeting))
}

func (ant *Ant) SendAck(target int) {
	go ant.Send(target, NewMessage(MessageAck))
}

func (ant *Ant) GetGreetingTarget() int {
	return ant.random.Int() % AntsCount
}

const AntsCount = 10000
const GreetingCount = 5000 * 2
const TestDurationSeconds = 10 * time.Second
const ProcessorsCount = 4

var Ants []*Ant

func main() {

	runtime.GOMAXPROCS(ProcessorsCount)

	Ants = make([]*Ant, AntsCount)
	for vid := range Ants {
		Ants[vid] = NewAnt(vid)
		go Ants[vid].Run()
	}

	fmt.Printf("Ants: %d\n", AntsCount)
	fmt.Printf("Greeting seeds: %d\n", GreetingCount)
	fmt.Printf("Test duration: %d s\n", int(TestDurationSeconds.Seconds()))
	fmt.Printf("Processors: %d\n", runtime.GOMAXPROCS(-1))
	fmt.Printf("\n")

	start := time.Now()

	go func() {
		for {
			for i := 0; i < GreetingCount; i++ {
				target := rand.Int() % AntsCount
				Ants[target].ch <- NewMessage(MessageSchedule)
			}
		}
	}()
	time.Sleep(TestDurationSeconds)
	runDuration := time.Now().Sub(start).Seconds()

	var duration float64
	var durationTotal int
	var greetingSent, greetingReceived int
	var rtt, rttMax, rttMin int64
	var rttTotal int
	var messages int

	rttMin = math.MaxInt64

	for _, ant := range Ants {
		if !ant.startCompleteTime.IsZero() {
			duration += float64(ant.startCompleteTime.Sub(ant.startTime))
			durationTotal++
		}
		greetingSent += ant.greetingsSent
		greetingReceived += ant.greetingsReceived
		for _, r := range ant.greetingRTTs {
			rtt += r
			rttTotal++
			if r > rttMax {
				rttMax = r
			}
			if r < rttMin {
				rttMin = r
			}
		}
		messages += ant.greetingsReceived + ant.AcksReceived
	}

	fmt.Printf("Start duration (avg): %d ms\n", time.Duration(duration/float64(durationTotal)).Milliseconds())
	fmt.Printf("\n")

	fmt.Printf("Greeting sent (avg): %v\n", greetingSent/AntsCount)
	fmt.Printf("Greeting sent (sum): %v\n", greetingSent)
	fmt.Printf("Greeting received (avg): %v\n", greetingReceived/AntsCount)
	fmt.Printf("Greeting received (sum): %v\n", greetingReceived)
	fmt.Printf("Messages: %d\n", messages)
	fmt.Printf("Message rate (m/s): %d\n", int(float64(messages)/runDuration))
	fmt.Printf("\n")

	fmt.Printf("Greeting RTT (avg): %f\n", time.Duration(rtt/int64(rttTotal)).Seconds())
	fmt.Printf("Greeting RTT (max): %f\n", time.Duration(rttMax).Seconds())
	fmt.Printf("Greeting RTT (min): %f\n", time.Duration(rttMin).Seconds())
	fmt.Printf("\n")
	
	fmt.Println("Completed.")
}
