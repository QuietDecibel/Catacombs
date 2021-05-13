#include "startwindow.h"
#include "ui_startwindow.h"
#include "PlaySquare.h"
#include "Dungeonspawner.h"
#include "monster.h"
#include <QGraphicsScene>
#include <QWindow>
#include "Questions.h"
#include <QVector>
#include <QRandomGenerator>
#include <QDebug>
#include <QPixmap>


extern QGraphicsScene *theWorld;
extern QGraphicsView *Level;
extern PlaySquare *playR;
extern Monster * Enemy;

startwindow::startwindow(QWidget *parent) :
    QMainWindow(parent),
    ui(new Ui::startwindow)
{
    DungeonSpawner *DMSGame = new DungeonSpawner();
    ui->setupUi(this);
    ui->Start->activateWindow();
    ui->stackedWidget->setCurrentIndex(0);
    ui->stackedWidget->setEnabled(true);
    QPixmap StartImage("C:/Users/Merc Kiv/OneDrive/Desktop/Object Oriented Programming in C++/Programming Assignments/Project Dungeon Spawner/assets/DungeonCastle.jpg");
    QPixmap CharacterImage("C:/Users/Merc Kiv/OneDrive/Desktop/Object Oriented Programming in C++/Programming Assignments/Project Dungeon Spawner/assets/Zolo.jpg");
    ui->StartPicture->setPixmap(StartImage);
    ui->PicChar->setPixmap(CharacterImage);

    Questions *Quest0,*Quest1,*Quest2,*Quest3,*Quest4,*Quest5,*Quest6,*Quest7,*Quest8,*Quest9;

    Quest0 = new Questions();
    QuestList.push_back(Quest0);
    Quest0->setQ1("Where would you most like to live?");
    Quest0->setA1("Urban.");
    Quest0->setA2("Rural.");
    Quest0->setA3("Suburban.");
    Quest0->setA4("A Cave.");

    Quest1 = new Questions();
    QuestList.push_back(Quest1);
    Quest1->setQ1("What element do you relate to most?");
    Quest1->setA1("Earth.");
    Quest1->setA2("Wind.");
    Quest1->setA3("Fire.");
    Quest1->setA4("Water.");

    Quest2 = new Questions();
    QuestList.push_back(Quest2);
    Quest2->setQ1("What kind of weapon would you choose?");
    Quest2->setA1("Close Range.");
    Quest2->setA2("Mid Range.");
    Quest2->setA3("Long Range.");
    Quest2->setA4("Super Long Range");

    Quest3 = new Questions();
    QuestList.push_back(Quest3);
    Quest3->setQ1("Who would you be in a group?");
    Quest3->setA1("Damage Dealer.");
    Quest3->setA2("Tank.");
    Quest3->setA3("Magician.");
    Quest3->setA4("Healer.");

    Quest4 = new Questions();
    QuestList.push_back(Quest4);
    Quest4->setQ1("Would you rather fight Dragon Sized Chickens or Chicken sized Dragons?");
    Quest4->setA1("Dragon Sized Chickens.");
    Quest4->setA2("Chicken Sized Dragons.");
    Quest4->setA3("Just Dragons - Regular.");
    Quest4->setA4("Both of 'Em can come at me.");

    Quest5 = new Questions();
    QuestList.push_back(Quest5);
    Quest5->setQ1("How does the sun work?");
    Quest5->setA1("The moon hides it.");
    Quest5->setA2("The sun hides from the earth behind the moon.");
    Quest5->setA3("Apollo and all those mountain dwellers.");
    Quest5->setA4("Doesnt matter whats next.");

    Quest6 = new Questions();
    QuestList.push_back(Quest6);
    Quest6->setQ1("Which is more your style?");
    Quest6->setA1("Research.");
    Quest6->setA2("Working with your hands.");
    Quest6->setA3("A mix of both.");
    Quest6->setA4("None, I'll sit back here.");

    Quest7 = new Questions();
    QuestList.push_back(Quest7);
    Quest7->setQ1("How fast do you walk?");
    Quest7->setA1("Slow.");
    Quest7->setA2("Mild.");
    Quest7->setA3("Fast.");
    Quest7->setA4("Always running.");

    Quest8 = new Questions();
    QuestList.push_back(Quest8);
    Quest8->setQ1("When you you sleep?");
    Quest8->setA1("At night.");
    Quest8->setA2("During the Day.");
    Quest8->setA3("Never.");
    Quest8->setA4("I'm dreaming right now.");

    Quest9 = new Questions();
    QuestList.push_back(Quest9);
    Quest9->setQ1("In what era did the Warring States Happen?");
    Quest9->setA1("Medival Times.");
    Quest9->setA2("Zhao Dynasty.");
    Quest9->setA3("Edo Period.");
    Quest9->setA4("The Renaissance.");

    //Sets the first question and answers to the labels.
    ui->QuestionsBox->setText(QuestList[i]->getQ1());
    ui->A1->setText(QuestList[i]->getA1());
    ui->A2->setText(QuestList[i]->getA2());
    ui->A3->setText(QuestList[i]->getA3());
    ui->A4->setText(QuestList[i]->getA4());
    i++;
    ui->Progressbar->setValue(0);
    qDebug() << "Inside Constructor: " << QuestList[3]->getA4();

    //The Character Profile Implementation of the Character information in the widgets.
    ui->CharacterName->setText(DMSGame->playR->getName());
    ui->Class->setText(DMSGame->playR->getJobClass());
    QListWidgetItem *P_Health = new QListWidgetItem(QString::number(DMSGame->playR->getHealth()));
    ui->PlayerStatistics->addItem(P_Health);
    QListWidgetItem *P_Magic = new QListWidgetItem(QString::number(DMSGame->playR->getMagic()));
    ui->PlayerStatistics->addItem(P_Magic);
    QListWidgetItem *P_Attack = new QListWidgetItem(QString::number(DMSGame->playR->getAttack()));
    ui->PlayerStatistics->addItem(P_Attack);
    QListWidgetItem *P_Defence = new QListWidgetItem(QString::number(DMSGame->playR->getDefense()));
    ui->PlayerStatistics->addItem(P_Defence);
    QListWidgetItem *P_Critical = new QListWidgetItem(QString::number(DMSGame->playR->getCritical()));
    ui->PlayerStatistics->addItem(P_Critical);

    QListWidgetItem *A_Head = new QListWidgetItem(QString::number(DMSGame->playR->FullSet.getHead()));
    ui->ArmorStatistics->addItem(A_Head);
    QListWidgetItem *A_Chest = new QListWidgetItem(QString::number(DMSGame->playR->FullSet.getChest()));
    ui->ArmorStatistics->addItem(A_Chest);
    QListWidgetItem *A_Legs = new QListWidgetItem(QString::number(DMSGame->playR->FullSet.getLegs()));
    ui->ArmorStatistics->addItem(A_Legs);
    QListWidgetItem *A_Feet = new QListWidgetItem(QString::number(DMSGame->playR->FullSet.getFeet()));
    ui->ArmorStatistics->addItem(A_Feet);
    QListWidgetItem *A_Hands = new QListWidgetItem(QString::number(DMSGame->playR->FullSet.getHands()));
    ui->ArmorStatistics->addItem(A_Hands);
    QListWidgetItem *A_Arms = new QListWidgetItem(QString::number(DMSGame->playR->FullSet.getArms()));
    ui->ArmorStatistics->addItem(A_Arms);
}

startwindow::~startwindow()
{
    delete ui;
}



void startwindow::on_QuitButton_clicked()
{
    exit(0);
}

void startwindow::on_QuitButton_2_clicked()
{
        exit(0);
}

void startwindow::on_stackedWidget_currentChanged(int arg1)
{

}

void startwindow::on_QuestionsBox_textChanged()
{

}

void startwindow::on_PointAwards_overflow()
{
   // Removed Mini Feature
}

void startwindow::on_StartButton_clicked()
{
    ui->stackedWidget->setCurrentIndex(1);
    ui->stackedWidget->setEnabled(true);
};

void startwindow::on_NextQButton_clicked()
{
    if(ui->Progressbar->value() < 100){
        //Increase ProgressBar by 10%
        ui->Progressbar->setValue(ui->Progressbar->value() + 10);

        //Sets the New Question and Answers into Text
        qDebug() << "Inside Next Button Function: " << QuestList[1]->getA4();
        qDebug() << "The Value of I is: " << i;
        ui->QuestionsBox->setText(QuestList[i]->getQ1());
        ui->A1->setText(QuestList[i]->getA1());
        ui->A2->setText(QuestList[i]->getA2());
        ui->A3->setText(QuestList[i]->getA3());
        ui->A4->setText(QuestList[i]->getA4());
        i++;

        //Calculates Random numbers per answer and that goes to a Player Stat
        QRandomGenerator *StatGenerator = new QRandomGenerator();
        DMSGame->playR->setHealth(StatGenerator->global()->bounded(20,50));
        DMSGame->playR->setMagic(StatGenerator->global()->bounded(20,60));
        DMSGame->playR->setAttack(StatGenerator->global()->bounded(20,50));
        DMSGame->playR->setDefense(StatGenerator->global()->bounded(20,55));
        DMSGame->playR->setCritical(StatGenerator->global()->bounded(20,50));
        delete StatGenerator;

        //Adds the new points to the Point counter, for Points awarded to player.
        ui->PointAwards->setDigitCount(DMSGame->playR->getHealth());
        return;
    } else{
        ui->stackedWidget->setCurrentIndex(3);
        ui->stackedWidget->setEnabled(true);
    }
};

void startwindow::on_Profile_2_clicked()
{
    ui->stackedWidget->setCurrentIndex(2);
    ui->stackedWidget->setEnabled(true);
};

void startwindow::on_CreatorWindowButton_clicked()
{
    ui->stackedWidget->setCurrentIndex(1);
    ui->stackedWidget->setEnabled(true);
};

void startwindow::on_PlayButton_clicked()
{
    ui->stackedWidget->setCurrentIndex(3);
    ui->stackedWidget->setEnabled(true);

};

void startwindow::on_PlayGame_clicked()
{


};

void startwindow::on_GameScreen_2_frameSwapped()
{

};

void startwindow::on_StatPageButton_clicked()
{
    ui->stackedWidget->setCurrentIndex(2);
    ui->stackedWidget->setEnabled(true);
}

void startwindow::on_Q_2_Pofile_clicked()
{
    ui->stackedWidget->setCurrentIndex(2);
    ui->stackedWidget->setEnabled(true);
}
